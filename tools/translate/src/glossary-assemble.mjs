// Join translated chunks (translate-chunks/chunk-NNN.json) back onto glossary.zh.json → glossary.ar.json, with a report.
import fs from "node:fs";
import path from "node:path";

export function assemble(dir) {
  const zh = JSON.parse(fs.readFileSync(path.join(dir, "glossary.zh.json"), "utf8")).terms;
  const chunkDir = path.join(dir, "translate-chunks");
  const translated = fs
    .readdirSync(chunkDir)
    .filter((f) => /^chunk-\d+\.json$/.test(f))
    .flatMap((f) => JSON.parse(fs.readFileSync(path.join(chunkDir, f), "utf8")).terms);
  // overrides.json (from glossary-fix.mjs) wins over the chunk translations.
  const overridesFile = path.join(dir, "overrides.json");
  const overrides = fs.existsSync(overridesFile) ? JSON.parse(fs.readFileSync(overridesFile, "utf8")).terms : [];
  const byTerm = new Map([...translated, ...overrides].map((t) => [t.term, t]));

  const merged = zh.map((t) => {
    const a = byTerm.get(t.term);
    return { ...t, arabic: a?.arabic ?? null, aliases_ar: Object.fromEntries((a?.aliases ?? []).map((x) => [x.alias, x.arabic])), brief_ar: a?.brief_ar ?? null };
  });
  const missing = merged.filter((t) => !t.arabic).map((t) => t.term);
  const missingAliases = merged.flatMap((t) => t.aliases.filter((a) => !t.aliases_ar[a]).map((a) => `${t.term}/${a}`));
  // Two different entities with the same Arabic form would merge in the reader's mind.
  const owners = new Map();
  merged.forEach((t) => t.arabic && owners.set(t.arabic, [...(owners.get(t.arabic) ?? []), t.term]));
  const collisions = [...owners].filter(([, ts]) => ts.length > 1);

  fs.writeFileSync(path.join(dir, "glossary.ar.json"), JSON.stringify({ terms: merged }, null, 2));
  fs.writeFileSync(path.join(dir, "translate-report.json"), JSON.stringify({ missing, missingAliases, collisions }, null, 2));
  return { terms: merged.length, missing: missing.length, missingAliases: missingAliases.length, collisions: collisions.length };
}
