// Build the Arabic novel JSON in the same shape as the scraped source: { Name, Volumes: [{ Title, Chapters: [{ Title, Body }] }] }.
// Refuses to build while any chapter of the requested volumes is untranslated.
//
// usage: node src/build-novel.mjs --novel death-sutra --volumes 1-3
import fs from "node:fs";
import path from "node:path";
import { arg, novel } from "./config.mjs";
import { chapterFile } from "./chapter-translation.mjs";

const n = novel(arg("novel"));
const source = JSON.parse(fs.readFileSync(n.source, "utf8"));
const [first, last] = arg("volumes", `1-${source.Volumes.length}`).split("-").map(Number);
if (!n.arabic?.volumes || n.arabic.volumes.length < last) throw new Error(`add Arabic titles for volumes up to ${last} in config.mjs (novels["${n.slug}"].arabic)`);

// Chapter indexes are global across volumes, as in the rest of the pipeline.
let index = source.Volumes.slice(0, first - 1).reduce((s, v) => s + v.Chapters.length, 0);
const missing = [];
const volumes = source.Volumes.slice(first - 1, last).map((v, k) => ({
  Title: n.arabic.volumes[first - 1 + k],
  Chapters: v.Chapters.map(() => {
    const file = chapterFile(n, index++, "json");
    if (!fs.existsSync(file)) return missing.push(index - 1), null;
    const c = JSON.parse(fs.readFileSync(file, "utf8"));
    // Older translations sometimes used straight quotes for nested quotes; normalize pairs to “ ” as prompt v5 asks.
    const quotes = (p) => p.replace(/"([^"]*)"/g, "“$1”");
    return { Title: c.title, Body: c.paragraphs.map(quotes) };
  }),
}));
if (missing.length) {
  console.error(`not built: ${missing.length} chapters untranslated: ${missing.join(" ")}`);
  process.exit(1);
}

const out = path.join(n.work, `${n.arabic.name} - vol ${first}-${last}.json`);
fs.writeFileSync(out, JSON.stringify({ Name: n.arabic.name, Volumes: volumes }, null, 2));
const chapters = volumes.reduce((s, v) => s + v.Chapters.length, 0);
const paragraphs = volumes.reduce((s, v) => s + v.Chapters.reduce((t, c) => t + c.Body.length, 0), 0);
console.log(`built ${out}\n${volumes.length} volumes, ${chapters} chapters, ${paragraphs} paragraphs, ${(fs.statSync(out).size / 1e6).toFixed(1)} MB`);
