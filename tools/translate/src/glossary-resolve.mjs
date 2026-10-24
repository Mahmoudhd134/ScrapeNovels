// Resolve what the automatic merge cannot: probable duplicates, gender conflicts, and frequent terms seen by
// only one window. Code finds the candidates, one Claude Code agent decides (checking the novel text), code applies.
// The merged glossary is kept as glossary.zh.merged.json; glossary.zh.json becomes the resolved one.
//
// usage: node src/glossary-resolve.mjs --novel death-sutra [--model opus] [--effort medium]
import fs from "node:fs";
import path from "node:path";
import { runAgent } from "./agent-pool.mjs";
import { arg, loadChapters, novel, toolDir } from "./config.mjs";

const n = novel(arg("novel"));
const dir = path.join(n.work, "glossary");
const resolveDir = path.join(dir, "resolve");
fs.mkdirSync(resolveDir, { recursive: true });
const mergedFile = path.join(dir, "glossary.zh.merged.json");
if (!fs.existsSync(mergedFile)) fs.copyFileSync(path.join(dir, "glossary.zh.json"), mergedFile);
const entries = JSON.parse(fs.readFileSync(mergedFile, "utf8")).terms;

// Refs are "term|type"; a repeated ref gets a #n suffix so every entry has a unique id.
const seen = {};
entries.forEach((e) => {
  const base = `${e.term}|${e.type}`;
  seen[base] = (seen[base] ?? 0) + 1;
  e.ref = seen[base] > 1 ? `${base}#${seen[base]}` : base;
});
const view = (e) => ({ ref: e.ref, aliases: e.aliases, gender: e.gender, briefs: e.briefs, occurrences: e.occurrences, windows: e.windows, first_chapter: e.first_chapter });

// 1. Candidates.
const chapters = loadChapters(n);
const fullText = chapters.map((c) => [c.Title, ...c.Body].join("\n")).join("\n");
const duplicates = [];
const people = entries.filter((e) => e.type === "person");
for (const a of people)
  for (const b of people)
    if (a !== b && a.term.length >= 2 && b.term.length > a.term.length && b.term.includes(a.term) && !b.aliases.includes(a.term))
      duplicates.push({ kind: "name inside another name", entries: [view(a), view(b)] });
const byTerm = new Map();
entries.forEach((e) => byTerm.set(e.term, [...(byTerm.get(e.term) ?? []), e]));
for (const [, es] of byTerm) if (es.length > 1) duplicates.push({ kind: "same term, several entries", entries: es.map(view) });

// Pronoun right after a name (within 30 characters) — a rough signal of how the narration treats the character.
function pronouns(e) {
  let he = 0;
  let she = 0;
  for (const name of [e.term, ...e.aliases].filter((x) => x.length >= 2)) {
    let i = fullText.indexOf(name);
    while (i >= 0) {
      const after = fullText.slice(i + name.length, i + name.length + 30);
      const h = after.indexOf("他");
      const s = after.indexOf("她");
      if (h >= 0 && (s < 0 || h < s)) he++;
      else if (s >= 0) she++;
      i = fullText.indexOf(name, i + name.length);
    }
  }
  return { he, she };
}
const gender = entries.filter((e) => e.gender_conflict).map((e) => ({ ...view(e), pronouns_after_name: pronouns(e) }));

const inCandidates = new Set([...duplicates.flatMap((d) => d.entries.map((x) => x.ref)), ...gender.map((g) => g.ref)]);
const suspicious = entries.filter((e) => e.windows === 1 && e.occurrences >= 5 && !inCandidates.has(e.ref)).map(view);

const candidatesFile = path.join(resolveDir, "candidates.json");
const decisionsFile = path.join(resolveDir, "decisions.json");
const textFile = path.join(resolveDir, "novel.txt");
fs.writeFileSync(candidatesFile, JSON.stringify({ duplicates, gender, suspicious }, null, 1));
fs.writeFileSync(textFile, chapters.map((c, i) => `<chapter index="${i}" title="${c.Title}">\n${c.Body.join("\n")}\n</chapter>`).join("\n\n"));
console.log(`candidates: ${duplicates.length} duplicate groups, ${gender.length} gender conflicts, ${suspicious.length} suspicious`);

// 2. Agent decides (skipped when decisions already exist).
if (!fs.existsSync(decisionsFile)) {
  const instructions = path.join(toolDir, "prompts", "resolve-glossary.md");
  const run = await runAgent({
    prompt: [`Follow the instructions in this file exactly: ${instructions}`, `Candidates: ${candidatesFile}`, `Novel text: ${textFile}`, `Write the decisions JSON to: ${decisionsFile}`].join("\n"),
    cwd: resolveDir,
    addDirs: [resolveDir, path.dirname(instructions)],
    model: arg("model", "opus"),
    effort: arg("effort", "medium"),
    logFile: path.join(resolveDir, "agent.log"),
  });
  console.log(`agent finished in ${run.seconds}s (exit ${run.code})`);
}
const d = JSON.parse(fs.readFileSync(decisionsFile, "utf8"));

// 3. Apply.
const byRef = new Map(entries.map((e) => [e.ref, e]));
const unknown = [];
const get = (ref) => byRef.get(ref) ?? (unknown.push(ref), null);
const removed = new Set();
for (const m of d.merge ?? []) {
  const into = get(m.into);
  if (!into) continue;
  for (const ref of m.absorb) {
    const x = get(ref);
    if (!x || x === into) continue;
    into.aliases = [...new Set([...into.aliases, x.term, ...x.aliases])].filter((a) => a !== into.term);
    into.briefs = [...new Set([...into.briefs, ...x.briefs])].slice(0, 3);
    into.windows += x.windows;
    removed.add(x.ref);
  }
}
for (const g of d.gender ?? []) {
  const e = get(g.ref);
  if (!e) continue;
  e.gender = g.gender;
  if (g.note) e.gender_note = g.note;
  delete e.gender_conflict;
}
for (const x of d.drop ?? []) if (get(x.ref)) removed.add(x.ref);

const resolved = entries.filter((e) => !removed.has(e.ref)).map(({ ref, ...e }) => e);
fs.writeFileSync(path.join(dir, "glossary.zh.json"), JSON.stringify({ terms: resolved }, null, 2));
const decided = new Set([...(d.merge ?? []).flatMap((m) => [m.into, ...m.absorb]), ...(d.keep_separate ?? []).flatMap((k) => k.refs), ...(d.gender ?? []).map((g) => g.ref), ...(d.drop ?? []).map((x) => x.ref)]);
const undecided = [...inCandidates, ...suspicious.map((s) => s.ref)].filter((r) => !decided.has(r));
console.log(`merge ${d.merge?.length ?? 0} | keep separate ${d.keep_separate?.length ?? 0} | gender ${d.gender?.length ?? 0} (varies: ${(d.gender ?? []).filter((g) => g.gender === "varies").length}) | drop ${d.drop?.length ?? 0}`);
console.log(`${entries.length} → ${resolved.length} entries | unknown refs: ${unknown.length} ${unknown.slice(0, 10).join(" ")} | undecided candidates: ${undecided.length}`);
