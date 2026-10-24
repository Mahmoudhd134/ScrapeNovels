import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const toolDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
export const repoDir = path.resolve(toolDir, "../..");

// One entry per novel. `source` is the scraped JSON; everything the pipeline writes goes under `work`.
// `windowChars`: glossary windows by character budget (~40K tokens) instead of 10 chapters, for uneven chapter sizes.
export const novels = {
  "death-sutra": {
    title: "死人经",
    source: "output/Death Sutra (死人经).json",
    // Arabic names for the built novel (build-novel.mjs); add volumes as they are translated.
    arabic: {
      name: "كتاب الموتى (Death Sutra)",
      volumes: ["المجلد الأول: الفتى القاتل", "المجلد الثاني: سيد التنانين", "المجلد الثالث: ظلال الليل الحالك"],
    },
  },
  "joy-of-life": { title: "庆余年", source: "output/Joy of Life (庆余年).json", windowChars: 33000 },
  nightfall: { title: "将夜", source: "output/Nightfall (将夜).json", windowChars: 33000 },
};

export function novel(slug) {
  const n = novels[slug];
  if (!n) throw new Error(`unknown novel "${slug}", known: ${Object.keys(novels).join(", ")}`);
  const work = path.join(repoDir, "output", "translate", slug);
  fs.mkdirSync(work, { recursive: true });
  return { slug, ...n, source: path.join(repoDir, n.source), work };
}

export function loadChapters(n) {
  const data = JSON.parse(fs.readFileSync(n.source, "utf8"));
  return data.Volumes ? data.Volumes.flatMap((v) => v.Chapters) : data.Chapters;
}

// API key: ANTHROPIC_API_KEY, or a file named by KEY_FILE, or tools/translate/.key (gitignored).
export function apiKey() {
  if (process.env.ANTHROPIC_API_KEY) return process.env.ANTHROPIC_API_KEY;
  const file = process.env.KEY_FILE ?? path.join(toolDir, ".key");
  if (fs.existsSync(file)) return fs.readFileSync(file, "utf8").trim();
  throw new Error("no API key: set ANTHROPIC_API_KEY or KEY_FILE, or create tools/translate/.key");
}

// $ per million tokens, standard rate. Batch is half.
export const prices = { "claude-haiku-5-5": [0.1, 0.5], "claude-sonnet-5-5": [2, 10], "claude-opus-5-5": [4, 20] };

export const arg = (name, fallback) => {
  const i = process.argv.indexOf(`--${name}`);
  return i > 0 ? process.argv[i + 1] : fallback;
};
