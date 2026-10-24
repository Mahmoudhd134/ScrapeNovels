// Merge per-window glossaries into one novel glossary.
// Entries that share a name (term or alias) are the same entity, with two guards against false merges:
//   - people, families and everything else are merged only within their own group;
//   - a one-character name links two entries only when it is the term of both.
//
// usage: node src/glossary-merge.mjs --novel death-sutra
import fs from "node:fs";
import path from "node:path";
import { arg, loadChapters, novel } from "./config.mjs";

const n = novel(arg("novel"));
const dir = path.join(n.work, "glossary");
const chapters = loadChapters(n);
const fullText = chapters.map((c) => [c.Title, ...c.Body].join("\n")).join("\n");

const windowFiles = fs.readdirSync(path.join(dir, "windows-out")).filter((f) => f.endsWith(".json")).sort();
const group = (type) => (type === "person" ? "person" : type === "family" ? "family" : "thing");

// 1. Load, keeping only names that occur in their window.
const records = [];
let droppedNames = 0;
for (const f of windowFiles) {
  const id = f.replace(".json", "");
  const text = fs.readFileSync(path.join(dir, "windows", `${id}.txt`), "utf8");
  for (const t of JSON.parse(fs.readFileSync(path.join(dir, "windows-out", f), "utf8")).terms) {
    if (!text.includes(t.term)) { droppedNames++; continue; }
    const aliases = (t.aliases ?? []).filter((a) => a !== t.term && (text.includes(a) || (droppedNames++, false)));
    records.push({ ...t, aliases, window: id });
  }
}

// 2. Union-find over records.
const parent = records.map((_, i) => i);
const find = (i) => (parent[i] === i ? i : (parent[i] = find(parent[i])));
const keyOwner = new Map();
records.forEach((r, i) => {
  for (const name of [r.term, ...r.aliases]) {
    if (name.length === 1 && name !== r.term) continue;
    const key = `${group(r.type)}|${name}${name.length === 1 ? "|term" : ""}`;
    if (keyOwner.has(key)) parent[find(i)] = find(keyOwner.get(key));
    else keyOwner.set(key, i);
  }
});

// 3. Build one entry per group of records.
const count = (s) => fullText.split(s).length - 1;
const majority = (values) => {
  const tally = {};
  values.forEach((v) => (tally[v] = (tally[v] ?? 0) + 1));
  return Object.entries(tally).sort((a, b) => b[1] - a[1])[0]?.[0];
};
const groups = new Map();
records.forEach((r, i) => {
  const root = find(i);
  if (!groups.has(root)) groups.set(root, []);
  groups.get(root).push(r);
});

const entries = [...groups.values()].map((rs) => {
  const termTally = majority(rs.map((r) => r.term));
  const names = [...new Set(rs.flatMap((r) => [r.term, ...r.aliases]))];
  // Canonical term: the term most windows used; ties go to the longest name.
  const term = rs
    .map((r) => r.term)
    .sort((a, b) => (b === termTally) - (a === termTally) || b.length - a.length)[0];
  const genders = rs.map((r) => r.gender).filter((g) => g === "male" || g === "female");
  const firstChapter = chapters.findIndex((c) => [c.Title, ...c.Body].join("\n").includes(term));
  const sentence = firstChapter < 0 ? "" : chapters[firstChapter].Body.find((p) => p.includes(term)) ?? "";
  return {
    term,
    type: majority(rs.map((r) => r.type)),
    gender: genders.length ? majority(genders) : majority(rs.map((r) => r.gender)),
    aliases: names.filter((x) => x !== term),
    briefs: [...new Set(rs.map((r) => r.brief))].slice(0, 3),
    occurrences: count(term),
    windows: [...new Set(rs.map((r) => r.window))].length,
    first_chapter: firstChapter,
    context: sentence.length > 160 ? sentence.slice(Math.max(0, sentence.indexOf(term) - 60), sentence.indexOf(term) + 100) : sentence,
    gender_conflict: new Set(genders).size > 1,
  };
});
entries.sort((a, b) => a.first_chapter - b.first_chapter || b.occurrences - a.occurrences);

// 4. Names claimed by entities in different groups (e.g. a family and a person) — worth a look.
const claims = new Map();
entries.forEach((e, i) => [e.term, ...e.aliases].forEach((x) => claims.set(x, [...(claims.get(x) ?? []), i])));
const shared = [...claims].filter(([, ids]) => ids.length > 1).map(([name, ids]) => ({ name, entries: ids.map((i) => `${entries[i].term} (${entries[i].type})`) }));

fs.writeFileSync(path.join(dir, "glossary.zh.json"), JSON.stringify({ terms: entries }, null, 2));
fs.writeFileSync(path.join(dir, "merge-report.json"), JSON.stringify({ windows: windowFiles.length, records: records.length, droppedNames, entries: entries.length, genderConflicts: entries.filter((e) => e.gender_conflict).map((e) => e.term), sharedNames: shared }, null, 2));
const byType = majority ? Object.entries(entries.reduce((a, e) => ((a[e.type] = (a[e.type] ?? 0) + 1), a), {})).map(([k, v]) => `${k} ${v}`).join(", ") : "";
console.log(`${windowFiles.length} windows, ${records.length} records → ${entries.length} entries (${byType})`);
console.log(`dropped non-verbatim names: ${droppedNames} | gender conflicts: ${entries.filter((e) => e.gender_conflict).length} | names shared across entries: ${shared.length}`);
