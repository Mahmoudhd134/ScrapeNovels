// Shared pieces of chapter translation: building the request, checking and saving the result.
import fs from "node:fs";
import path from "node:path";
import { z } from "zod";
import { zodOutputFormat } from "@anthropic-ai/sdk/helpers/zod";
import { toolDir } from "./config.mjs";

export const Output = z.object({ title: z.string(), paragraphs: z.array(z.string()) });
const contextShare = 0.3;

export function loadGlossary(n) {
  return JSON.parse(fs.readFileSync(path.join(n.work, "glossary", "glossary.ar.json"), "utf8")).terms;
}

export function systemPrompt(version) {
  return fs.readFileSync(path.join(toolDir, "prompts", `translate-${version}.txt`), "utf8");
}

// User message: glossary entries found in this chapter or its context, the end of the previous chapter (Chinese,
// context only), and the chapter itself with paragraphs separated by blank lines.
export function buildInput(chapters, glossary, i) {
  const chapter = chapters[i];
  const prev = chapters[i - 1];
  const context = prev ? prev.Body.slice(Math.floor(prev.Body.length * (1 - contextShare))) : [];
  const text = [chapter.Title, ...chapter.Body, ...context].join("\n");
  const entries = glossary
    .filter((t) => t.arabic && [t.term, ...t.aliases].some((name) => text.includes(name)))
    .map((t) => {
      const aliases = t.aliases.filter((a) => text.includes(a) && t.aliases_ar[a]).map((a) => `${a} → ${t.aliases_ar[a]}`);
      const gender = t.type === "person" && ["male", "female", "varies"].includes(t.gender) ? `, ${t.gender}` : "";
      return [`${t.term} → ${t.arabic} | ${t.type}${gender}`, t.brief_ar, t.gender_note ? `gender note: ${t.gender_note}` : null, aliases.length ? `aliases: ${aliases.join("، ")}` : null]
        .filter(Boolean)
        .join(" | ");
    });
  return (
    `<glossary>\n${entries.join("\n")}\n</glossary>\n\n` +
    (context.length ? `<previous_context>\n${context.join("\n\n")}\n</previous_context>\n\n` : "") +
    `<chapter title="${chapter.Title}">\n${chapter.Body.join("\n\n")}\n</chapter>`
  );
}

export function requestParams({ model, effort, system, input }) {
  return { model, max_tokens: 32000, output_config: { effort, format: zodOutputFormat(Output) }, system, messages: [{ role: "user", content: input }] };
}

// Parse and check a finished message against its source chapter.
export function check(message, sourceChapter) {
  if (message.stop_reason !== "end_turn") return { ok: false, reason: `stop_reason ${message.stop_reason}` };
  let out;
  try {
    out = Output.parse(JSON.parse(message.content.filter((b) => b.type === "text").map((b) => b.text).join("")));
  } catch (e) {
    return { ok: false, reason: `unparseable output: ${e.message.slice(0, 120)}` };
  }
  const source = sourceChapter.Body;
  if (out.paragraphs.length !== source.length) return { ok: false, reason: `paragraphs ${out.paragraphs.length}/${source.length}`, out };
  const chinese = out.paragraphs.filter((p) => /[一-鿿]/.test(p)).length;
  if (chinese) return { ok: false, reason: `${chinese} paragraphs with Chinese left`, out };
  const empty = out.paragraphs.filter((p) => !p.trim()).length;
  if (empty) return { ok: false, reason: `${empty} empty paragraphs`, out };
  // A stray " inside the JSON output closes the paragraph string early: the paragraph then stops mid-sentence
  // even when the count still matches. Flag paragraphs that end without punctuation where the source's does.
  const finished = /[.!?؟…»”"۔:：；;؛。！？—)\]〗※】]\s*$/;
  const cut = out.paragraphs.filter((p, k) => finished.test(source[k]) && !finished.test(p.trim())).length;
  if (cut) return { ok: false, reason: `${cut} paragraphs cut mid-sentence`, out };
  return { ok: true, out, ratio: +(out.paragraphs.join("").length / source.join("").length).toFixed(2) };
}

export function chapterFile(n, i, ext) {
  const dir = path.join(n.work, "chapters");
  fs.mkdirSync(dir, { recursive: true });
  return path.join(dir, `ch${String(i).padStart(4, "0")}.${ext}`);
}

export function save(n, i, sourceChapter, out, meta) {
  fs.writeFileSync(chapterFile(n, i, "json"), JSON.stringify({ index: i, title_zh: sourceChapter.Title, title: out.title, paragraphs: out.paragraphs, meta }, null, 1));
  fs.writeFileSync(chapterFile(n, i, "md"), `# ${out.title}\n\n${out.paragraphs.join("\n\n")}\n`);
}

export const isDone = (n, i) => fs.existsSync(chapterFile(n, i, "json"));
