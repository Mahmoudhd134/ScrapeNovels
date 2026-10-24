// One API pass over what the chunked translation left: terms or aliases without Arabic, and Arabic forms shared by
// several entries (homophone surnames, ranks). The model decides whether shared forms are fine (same entity, variant
// spelling) or must be told apart. Results go to glossary/overrides.json, which assemble applies on top of the chunks.
//
// usage: node src/glossary-fix.mjs --novel death-sutra [--model claude-opus-5-5] [--effort medium]
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
const dir = path.join(n.work, "glossary");
const client = new Anthropic({ apiKey: apiKey() });

const glossary = JSON.parse(fs.readFileSync(path.join(dir, "glossary.ar.json"), "utf8")).terms;
const report = JSON.parse(fs.readFileSync(path.join(dir, "translate-report.json"), "utf8"));
const info = (t) => ({ term: t.term, type: t.type, gender: t.gender, arabic: t.arabic, aliases: t.aliases, aliases_ar: t.aliases_ar, briefs: t.briefs, brief_ar: t.brief_ar });

const missingTerms = report.missing.map((term) => info(glossary.find((t) => t.term === term)));
const missingAliases = [...new Set(report.missingAliases.map((x) => x.split("/")[0]))].map((term) => info(glossary.find((t) => t.term === term)));
// Collisions between entries with the same term are duplicates of one entity; skip those.
const collisions = report.collisions.filter(([, terms]) => new Set(terms).size > 1).map(([arabic, terms]) => ({ arabic, entries: [...new Set(terms)].map((term) => info(glossary.find((t) => t.term === term))) }));

const system = `${fs.readFileSync(path.join(toolDir, "prompts", "translate-glossary.txt"), "utf8")}

This is a repair pass over an existing glossary. You receive:
- <missing_terms>: entries with no Arabic form yet. Give each an Arabic form, alias forms and brief_ar.
- <missing_aliases>: entries whose listed aliases lack Arabic forms. Give the missing alias forms (keep the existing Arabic form unless it is wrong).
- <collisions>: groups of different Chinese terms that received the same Arabic form. For each group decide:
  - same entity or harmless variant (a variant character, a synonym rank, a country and its short name): keep the shared form;
  - different entities (two different people, two families with homophone surnames, two different ranks): give each a distinct Arabic form a reader can tell apart, changing as little as possible. For homophone surnames, vary the spelling slightly but consistently (e.g. لوه vs لوو), and keep the more frequent or earlier entity's current form.
Return only the entries you change or fill in. For each, return the full entry: term, arabic, all aliases with Arabic, brief_ar.`;

const Result = z.object({
  terms: z.array(z.object({ term: z.string(), arabic: z.string(), aliases: z.array(z.object({ alias: z.string(), arabic: z.string() })), brief_ar: z.string() })),
  decisions: z.array(z.object({ arabic: z.string(), decision: z.enum(["keep_shared", "split"]), reason: z.string() })),
});
const input = `<missing_terms>\n${JSON.stringify(missingTerms, null, 1)}\n</missing_terms>\n\n<missing_aliases>\n${JSON.stringify(missingAliases, null, 1)}\n</missing_aliases>\n\n<collisions>\n${JSON.stringify(collisions, null, 1)}\n</collisions>`;

const response = await client.messages.stream({ model, max_tokens: 64000, output_config: { effort, format: zodOutputFormat(Result) }, system, messages: [{ role: "user", content: input }] }).finalMessage();
const u = response.usage;
const result = Result.parse(JSON.parse(response.content.filter((b) => b.type === "text").map((b) => b.text).join("")));

const overridesFile = path.join(dir, "overrides.json");
const overrides = fs.existsSync(overridesFile) ? JSON.parse(fs.readFileSync(overridesFile, "utf8")) : { terms: [] };
const byTerm = new Map(overrides.terms.map((t) => [t.term, t]));
result.terms.forEach((t) => byTerm.set(t.term, t));
fs.writeFileSync(overridesFile, JSON.stringify({ terms: [...byTerm.values()], decisions: result.decisions }, null, 2));

const after = assemble(dir);
console.log(`fixed ${result.terms.length} entries | collisions: ${result.decisions.filter((d) => d.decision === "split").length} split, ${result.decisions.filter((d) => d.decision === "keep_shared").length} kept | $${((u.input_tokens * prices[model][0] + u.output_tokens * prices[model][1]) / 1e6).toFixed(3)}`);
console.log(`now: missing arabic ${after.missing} | missing alias arabic ${after.missingAliases} | collisions ${after.collisions}`);
