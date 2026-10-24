# Glossary resolve instructions

A glossary was extracted from a Chinese novel by many readers, each reading a different window of chapters, then merged automatically. The merge could not fix a few kinds of problems. You review only those candidates and decide. Do the work yourself; do NOT call any AI API. Use scripts freely to search the novel text (it is large; search it, don't read it whole) and to write the JSON.

Inputs (paths are in the task message):
- `candidates.json`: the problem cases, each with entry refs (`term|type`), briefs, aliases, occurrence counts, and for gender cases the count of 他/她 found right after the name.
- the novel text, one chapter per `<chapter>` block, to check how names are really used.

## Decide

1. **duplicates**: two entries that may be the same entity (one name inside another, or the same term filed under two types).
   - Same entity → merge. Keep the fuller or more specific entry.
   - Different entities (楼兰王 the king vs 楼兰王子 the prince; 上官雨 vs 上官雨时 if they are different people) → keep both.
   - A person and a title with the same name: if the title is only ever used for one person, merge into the person; if several people hold it over the story, keep the title separate.
2. **gender**: people whose readers disagreed.
   - Check the text. If one gender is right and the other a reader's mistake → that gender.
   - If the story really changes it (a disguise, a hidden identity, a reveal) → `"varies"`, with a short Chinese note saying when and how (e.g. 女扮男装，前期以男子身份出现，第N章起身份揭开).
3. **suspicious**: entries seen by only one reader although the term occurs often. Decide whether each is a real name that belongs in the glossary, or a generic word that should be dropped (common titles like 夫人/军师/族长, standard wuxia vocabulary like 走火入魔, and single characters that are only a surname already covered by family entries). Keep a single-character family or place name if the text really uses it alone as a name.

## Output
Write UTF-8 JSON with a script to the output path you were given:

```
{
  "merge": [{"into": "term|type", "absorb": ["term|type", ...], "reason": "..."}],
  "keep_separate": [{"refs": ["term|type", "term|type"], "reason": "..."}],
  "gender": [{"ref": "term|type", "gender": "male|female|varies", "note": "Chinese note, required for varies"}],
  "drop": [{"ref": "term|type", "reason": "..."}]
}
```

Every candidate must end up in exactly one decision. Use refs exactly as written in `candidates.json`.

Reply with one line: counts of merge / keep_separate / gender / drop.
