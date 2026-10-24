// Translate the merged Chinese glossary into Arabic with the API, in chunks. Resumable per chunk.
// (Alternative to glossary-translate-agent.mjs, which does the same with Claude Code agents.)
//
// usage: node src/glossary-translate.mjs --novel death-sutra [--model claude-opus-5-5] [--effort medium] [--chunk 100] [--parallel 4]
import fs from "node:fs";
import path from "node:path";
import Anthropic from "@anthropic-ai/sdk";
import { z } from "zod";
import { zodOutputFormat } from "@anthropic-ai/sdk/helpers/zod";
import { apiKey, arg, novel, prices, toolDir } from "./config.mjs";
import { assemble } from "./glossary-assemble.mjs";

const n = novel(arg("novel"));
const model = arg("model", "claude-opus-5-5");
const effort = arg("effort", "medium");
const chunkSize = Number(arg("chunk", 100));
const parallel = Number(arg("parallel", 4));
const dir = path.join(n.work, "glossary");
const chunkDir = path.join(dir, "translate-chunks");
fs.mkdirSync(chunkDir, { recursive: true });

const client = new Anthropic({ apiKey: apiKey() });
const system = fs.readFileSync(path.join(toolDir, "prompts", "translate-glossary.txt"), "utf8");
const Result = z.object({
  terms: z.array(z.object({ term: z.string(), arabic: z.string(), aliases: z.array(z.object({ alias: z.string(), arabic: z.string() })), brief_ar: z.string() })),
});

const zh = JSON.parse(fs.readFileSync(path.join(dir, "glossary.zh.json"), "utf8")).terms;
// Chunks keep the glossary's order (first appearance), so related entities tend to share a chunk.
const chunks = [];
for (let i = 0; i < zh.length; i += chunkSize) chunks.push(zh.slice(i, i + chunkSize));

let cost = 0;
async function translateChunk(k) {
  const file = path.join(chunkDir, `chunk-${String(k).padStart(3, "0")}.json`);
  if (fs.existsSync(file)) return JSON.parse(fs.readFileSync(file, "utf8"));
  const input = JSON.stringify(chunks[k].map(({ term, type, gender, gender_note, aliases, briefs, occurrences, context }) => ({ term, type, gender, gender_note, aliases, briefs, occurrences, context })), null, 1);
  // Later chunks see the names already fixed in the first chunk, so shared surnames keep one spelling.
  const established = k > 0 && results[0] ? `<established_names>\n${results[0].terms.filter((t) => ["person", "family"].includes(chunks[0].find((z) => z.term === t.term)?.type)).map((t) => `${t.term} → ${t.arabic}`).join("\n")}\n</established_names>\nThese names are already fixed. Reuse their spellings for the same surnames and syllables.\n\n` : "";
  const response = await client.messages
    .stream({ model, max_tokens: 64000, output_config: { effort, format: zodOutputFormat(Result) }, system, messages: [{ role: "user", content: `${established}<glossary>\n${input}\n</glossary>` }] })
    .finalMessage();
  const u = response.usage;
  const c = (u.input_tokens * prices[model][0] + u.output_tokens * prices[model][1]) / 1e6;
  cost += c;
  if (response.stop_reason !== "end_turn") throw new Error(`chunk ${k}: stop_reason ${response.stop_reason}`);
  const terms = Result.parse(JSON.parse(response.content.filter((b) => b.type === "text").map((b) => b.text).join(""))).terms;
  fs.writeFileSync(file, JSON.stringify({ usage: u, cost: c, terms }, null, 1));
  console.log(`chunk ${k + 1}/${chunks.length}: ${terms.length} terms, in ${u.input_tokens} out ${u.output_tokens}, $${c.toFixed(3)}`);
  return { terms };
}

const results = [];
results[0] = await translateChunk(0);
const queue = chunks.map((_, k) => k).slice(1);
await Promise.all(
  Array.from({ length: parallel }, async () => {
    while (queue.length) {
      const k = queue.shift();
      results[k] = await translateChunk(k);
    }
  }),
);

const report = assemble(dir);
console.log(`${report.terms} terms | missing arabic ${report.missing} | missing alias arabic ${report.missingAliases} | arabic collisions ${report.collisions} | this run $${cost.toFixed(2)} (standard rate)`);
