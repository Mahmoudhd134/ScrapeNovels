// Translate the merged Chinese glossary into Arabic with headless Claude Code agents, in chunks.
// The first chunk (main cast, by first appearance) runs alone; its names are then given to every other chunk
// so shared surnames keep one spelling. Resumable per chunk.
//
// usage: node src/glossary-translate-agent.mjs --novel death-sutra [--chunk 100] [--concurrency 20] [--model opus] [--effort medium]
import fs from "node:fs";
import path from "node:path";
import { pool, runAgent } from "./agent-pool.mjs";
import { arg, novel, toolDir } from "./config.mjs";
import { assemble } from "./glossary-assemble.mjs";

const n = novel(arg("novel"));
const chunkSize = Number(arg("chunk", 100));
const concurrency = Number(arg("concurrency", 20));
const model = arg("model", "opus");
const effort = arg("effort", "medium");
const maxAttempts = 3;

const dir = path.join(n.work, "glossary");
const chunkDir = path.join(dir, "translate-chunks");
const logDir = path.join(dir, "logs");
fs.mkdirSync(chunkDir, { recursive: true });
const rules = path.join(toolDir, "prompts", "translate-glossary.txt");
const establishedFile = path.join(chunkDir, "established-names.txt");

const zh = JSON.parse(fs.readFileSync(path.join(dir, "glossary.zh.json"), "utf8")).terms;
const chunks = [];
for (let i = 0, k = 0; i < zh.length; i += chunkSize, k++) {
  const id = String(k).padStart(3, "0");
  const terms = zh.slice(i, i + chunkSize);
  const input = path.join(chunkDir, `in-${id}.json`);
  fs.writeFileSync(input, JSON.stringify(terms.map(({ term, type, gender, aliases, briefs, occurrences, context }) => ({ term, type, gender, aliases, briefs, occurrences, context })), null, 1));
  chunks.push({ k, id, terms, input, out: path.join(chunkDir, `chunk-${id}.json`) });
}

// A chunk is done when every input term has an Arabic form written in Arabic script.
function validate(c) {
  if (!fs.existsSync(c.out)) return { ok: false, reason: "no output" };
  let data;
  try {
    data = JSON.parse(fs.readFileSync(c.out, "utf8"));
  } catch (e) {
    return { ok: false, reason: `bad json: ${e.message}` };
  }
  const got = new Map((data?.terms ?? []).map((t) => [t.term, t]));
  const missing = c.terms.filter((t) => !/[؀-ۿ]/.test(got.get(t.term)?.arabic ?? ""));
  if (missing.length) return { ok: false, reason: `${missing.length} terms without Arabic` };
  const aliasGaps = c.terms.reduce((s, t) => s + t.aliases.filter((a) => !(got.get(t.term).aliases ?? []).some((x) => x.alias === a && x.arabic)).length, 0);
  return { ok: true, terms: got.size, aliasGaps };
}

function runChunk(c, attempt) {
  const prompt = [
    `You are building an Arabic glossary. Do the work yourself; do NOT call any AI API and do not write scripts that translate. Use scripts only to write and check the JSON file.`,
    `1. Read the rules and follow them exactly: ${rules}`,
    c.k > 0 ? `2. Read the names already fixed by an earlier chunk, and reuse their spellings for the same surnames and syllables: ${establishedFile}` : `2. (This is the first chunk; there are no fixed names yet.)`,
    `3. Read the entries to translate: ${c.input}`,
    `4. Write UTF-8 JSON to ${c.out} with a script (JSON.stringify in node or json.dump(..., ensure_ascii=False)): {"terms": [{"term": string, "arabic": string, "aliases": [{"alias": string, "arabic": string}], "brief_ar": string}]} — every input entry, same order, every alias included.`,
    `5. Verify with a script that the file parses and that every input term and alias is present with a non-empty Arabic form.`,
    `Reply with one line only: "<number of terms> ok", or what failed.`,
  ].join("\n");
  return runAgent({ prompt, cwd: dir, addDirs: [dir, path.dirname(rules)], model, effort, logFile: path.join(logDir, `translate-${c.id}.attempt${attempt}.log`) });
}

async function translateChunk(c) {
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    if (validate(c).ok) break;
    const run = await runChunk(c, attempt);
    const v = validate(c);
    console.log(v.ok ? `chunk ${c.id} ok: ${v.terms} terms, ${v.aliasGaps} alias gaps, ${run.seconds}s, attempt ${attempt}` : `chunk ${c.id} attempt ${attempt} failed (${v.reason})`);
  }
}

console.log(`${zh.length} terms in ${chunks.length} chunks, ${chunks.filter((c) => validate(c).ok).length} already done`);
await translateChunk(chunks[0]);
if (!validate(chunks[0]).ok) throw new Error("first chunk failed; fix it before running the rest");
const first = JSON.parse(fs.readFileSync(chunks[0].out, "utf8")).terms;
const firstTypes = new Map(chunks[0].terms.map((t) => [t.term, t.type]));
fs.writeFileSync(establishedFile, first.filter((t) => ["person", "family"].includes(firstTypes.get(t.term))).map((t) => `${t.term} → ${t.arabic}`).join("\n"));

await pool(chunks.slice(1).filter((c) => !validate(c).ok), concurrency, translateChunk);

const report = assemble(dir);
console.log(`${report.terms} terms | missing arabic ${report.missing} | missing alias arabic ${report.missingAliases} | arabic collisions ${report.collisions}`);
