// Glossary extraction with headless Claude Code agents, always `--concurrency` running at once.
// Resumable: a window whose output already passes validation is skipped.
//
// usage: node src/glossary-extract.mjs --novel death-sutra [--window 10] [--concurrency 20]
//        [--model opus] [--effort medium] [--only w000,w001] [--limit 1]
import fs from "node:fs";
import path from "node:path";
import { pool, runAgent } from "./agent-pool.mjs";
import { arg, loadChapters, novel, toolDir } from "./config.mjs";

const n = novel(arg("novel"));
const windowSize = Number(arg("window", 10));
const concurrency = Number(arg("concurrency", 20));
const model = arg("model", "opus");
const effort = arg("effort", "medium");
const maxAttempts = 3;

const dir = path.join(n.work, "glossary");
const winDir = path.join(dir, "windows");
const outDir = path.join(dir, "windows-out");
const logDir = path.join(dir, "logs");
for (const d of [winDir, outDir, logDir]) fs.mkdirSync(d, { recursive: true });
const instructions = path.join(toolDir, "prompts", "extract-glossary.md");

// Group chapters into windows: a fixed chapter count, or (for novels with uneven chapters) a character budget,
// adding chapters until the next one would exceed it. Chapter indexes are global, so outputs trace back.
const chapters = loadChapters(n);
const windowChars = Number(arg("window-chars", n.windowChars ?? 0));
const groups = [];
for (let i = 0; i < chapters.length; ) {
  let end = i + 1;
  if (windowChars) {
    let size = chapters[i].Body.join("").length;
    while (end < chapters.length && size + chapters[end].Body.join("").length <= windowChars) size += chapters[end++].Body.join("").length;
  } else {
    end = Math.min(i + windowSize, chapters.length);
  }
  groups.push([i, end]);
  i = end;
}
const windows = groups.map(([start, end], k) => {
  const id = `w${String(k).padStart(3, "0")}`;
  const file = path.join(winDir, `${id}.txt`);
  const text = chapters
    .slice(start, end)
    .map((c, j) => `<chapter index="${start + j}" title="${c.Title}">\n${c.Body.join("\n")}\n</chapter>`)
    .join("\n\n");
  if (!fs.existsSync(file)) fs.writeFileSync(file, text);
  return { id, file, out: path.join(outDir, `${id}.json`), text };
});

// A window is done when its JSON parses and every term and alias occurs in the window text.
function validate(w) {
  if (!fs.existsSync(w.out)) return { ok: false, reason: "no output" };
  let data;
  try {
    data = JSON.parse(fs.readFileSync(w.out, "utf8"));
  } catch (e) {
    return { ok: false, reason: `bad json: ${e.message}` };
  }
  if (!Array.isArray(data?.terms) || data.terms.length === 0) return { ok: false, reason: "no terms" };
  const occurs = (s) => typeof s === "string" && s.length > 0 && w.text.includes(s);
  const bad = data.terms.filter((t) => !occurs(t.term) || !Array.isArray(t.aliases) || t.aliases.some((a) => !occurs(a)));
  return { ok: true, terms: data.terms.length, notVerbatim: bad.length };
}

function runWindow(w, attempt) {
  const prompt = [
    `Follow the instructions in this file exactly: ${instructions}`,
    `Input window: ${w.file}`,
    `Write the output JSON to: ${w.out}`,
  ].join("\n");
  return runAgent({ prompt, cwd: dir, addDirs: [dir, path.dirname(instructions)], model, effort, logFile: path.join(logDir, `${w.id}.attempt${attempt}.log`) });
}

let queue = windows.filter((w) => !validate(w).ok);
const alreadyDone = windows.length - queue.length;
const only = arg("only");
if (only) queue = queue.filter((w) => only.split(",").includes(w.id));
const limit = arg("limit");
if (limit) queue = queue.slice(0, Number(limit));
console.log(`${windows.length} windows, ${alreadyDone} already done, ${queue.length} to run, ${concurrency} at a time`);

let done = 0;
const failed = [];
await pool(queue, concurrency, async (w) => {
  let result;
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    const run = await runWindow(w, attempt);
    result = validate(w);
    if (result.ok) {
      console.log(`[${++done}] ${w.id} ok: ${result.terms} terms, ${result.notVerbatim} not verbatim, ${run.seconds}s, attempt ${attempt}`);
      break;
    }
    console.log(`${w.id} attempt ${attempt} failed (${result.reason}, exit ${run.code})`);
  }
  if (!result.ok) failed.push(w.id);
});
console.log(`finished: ${done} ok, ${failed.length} failed ${failed.join(" ")}`);
