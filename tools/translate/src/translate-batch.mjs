// Translate chapters through the Message Batches API (50% off). Chapters already translated are skipped,
// so `submit` after `collect` resubmits only what failed.
//
// usage:
//   node src/translate-batch.mjs submit  --novel death-sutra --from 0 --to 605 [--model claude-opus-5-5] [--effort low] [--prompt v4]
//   node src/translate-batch.mjs status  --novel death-sutra
//   node src/translate-batch.mjs collect --novel death-sutra
//   node src/translate-batch.mjs test    --novel death-sutra --chapters 0,1      (direct API calls, full price, for quick checks)
import fs from "node:fs";
import path from "node:path";
import Anthropic from "@anthropic-ai/sdk";
import { apiKey, arg, loadChapters, novel, prices } from "./config.mjs";
import { buildInput, check, isDone, loadGlossary, requestParams, save, systemPrompt } from "./chapter-translation.mjs";

const command = process.argv[2];
const n = novel(arg("novel"));
const client = new Anthropic({ apiKey: apiKey() });
const chapters = loadChapters(n);
const batchDir = path.join(n.work, "batches");
fs.mkdirSync(batchDir, { recursive: true });
const model = arg("model", "claude-opus-5-5");
const effort = arg("effort", "low");
const promptVersion = arg("prompt", "v4");
const customId = (i) => `ch${String(i).padStart(4, "0")}`;
const cost = (u, batch) => ((u.input_tokens * prices[model][0] + u.output_tokens * prices[model][1]) / 1e6) * (batch ? 0.5 : 1);

if (command === "submit") {
  const from = Number(arg("from", 0));
  const to = Number(arg("to", chapters.length - 1));
  const glossary = loadGlossary(n);
  const system = systemPrompt(promptVersion);
  const todo = [];
  for (let i = from; i <= Math.min(to, chapters.length - 1); i++) if (!isDone(n, i)) todo.push(i);
  if (!todo.length) {
    console.log("nothing to submit: every chapter in range is translated");
    process.exit(0);
  }
  const requests = todo.map((i) => ({ custom_id: customId(i), params: requestParams({ model, effort, system, input: buildInput(chapters, glossary, i) }) }));
  const batch = await client.messages.batches.create({ requests });
  fs.writeFileSync(path.join(batchDir, `${batch.id}.json`), JSON.stringify({ id: batch.id, created: new Date().toISOString(), model, effort, prompt: promptVersion, chapters: todo, collected: false }, null, 1));
  console.log(`submitted ${batch.id}: ${todo.length} chapters (${todo[0]}–${todo.at(-1)}), ${model} ${effort}, prompt ${promptVersion}`);
}

if (command === "status" || command === "collect") {
  const manifests = fs.readdirSync(batchDir).map((f) => JSON.parse(fs.readFileSync(path.join(batchDir, f), "utf8"))).filter((m) => !m.collected);
  if (!manifests.length) console.log("no open batches");
  for (const m of manifests) {
    const b = await client.messages.batches.retrieve(m.id);
    const c = b.request_counts;
    console.log(`${m.id}: ${b.processing_status} | processing ${c.processing}, succeeded ${c.succeeded}, errored ${c.errored}, expired ${c.expired}, canceled ${c.canceled}`);
    if (command !== "collect" || b.processing_status !== "ended") continue;

    let ok = 0;
    let spent = 0;
    const failures = [];
    for await (const r of await client.messages.batches.results(m.id)) {
      const i = Number(r.custom_id.slice(2));
      if (r.result.type !== "succeeded") {
        failures.push(`${r.custom_id}: ${r.result.type}${r.result.error ? " " + JSON.stringify(r.result.error).slice(0, 120) : ""}`);
        continue;
      }
      const msg = r.result.message;
      spent += cost(msg.usage, true);
      const result = check(msg, chapters[i]);
      if (!result.ok) {
        failures.push(`${r.custom_id}: ${result.reason}`);
        continue;
      }
      save(n, i, chapters[i], result.out, { batch: m.id, model: m.model, effort: m.effort, prompt: m.prompt, input: msg.usage.input_tokens, output: msg.usage.output_tokens, ratio: result.ratio });
      ok++;
    }
    m.collected = true;
    m.result = { ok, failed: failures.length, failures, cost: +spent.toFixed(2) };
    fs.writeFileSync(path.join(batchDir, `${m.id}.json`), JSON.stringify(m, null, 1));
    console.log(`collected: ${ok} saved, ${failures.length} failed, $${spent.toFixed(2)} (batch price)`);
    failures.forEach((f) => console.log(`  ${f}`));
    if (failures.length) console.log("rerun `submit` with the same range to retry the failed chapters");
  }
}

if (command === "test") {
  const glossary = loadGlossary(n);
  const system = systemPrompt(promptVersion);
  for (const i of arg("chapters").split(",").map(Number)) {
    const msg = await client.messages.stream(requestParams({ model, effort, system, input: buildInput(chapters, glossary, i) })).finalMessage();
    const result = check(msg, chapters[i]);
    if (result.ok) save(n, i, chapters[i], result.out, { test: true, model, effort, prompt: promptVersion, input: msg.usage.input_tokens, output: msg.usage.output_tokens, ratio: result.ratio });
    console.log(`${customId(i)}: ${result.ok ? "ok" : result.reason} | in ${msg.usage.input_tokens} out ${msg.usage.output_tokens} | $${cost(msg.usage, false).toFixed(3)}`);
  }
}
