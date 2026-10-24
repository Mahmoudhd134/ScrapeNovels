# Glossary extraction instructions

You are extracting a translation glossary from a window of chapters of a Chinese novel. Do the extraction yourself by reading the text carefully, in parts, and re-check your list against the text before finishing. Do NOT call any AI API and do not write scripts that call models. Use scripts only to write the JSON file and to verify that terms appear in the text.

Each chapter in the input is wrapped in `<chapter index="N" title="...">`. Read all of it; use offset/limit to read in parts.

## Output
Write UTF-8 JSON to the output path you were given, with a script (`JSON.stringify` in node, or `json.dump(..., ensure_ascii=False)` in python), not hand-typed JSON.

Schema: `{"terms": [{"term": string, "type": one of person|family|place|organization|faction|title|rank|item|skill|creature|race|event|other, "brief": string, "gender": male|female|unknown|not_applicable, "aliases": [string]}]}`
- term: the exact Chinese form as it appears in the text; prefer the fullest name, e.g. a person's full name
- brief: one short Chinese sentence: who or what it is and how it relates to other entities, from the text only
- aliases: other Chinese forms that name exactly this same entity, each appearing in the text

## What to list
The novel will be translated into Arabic and every name must be rendered consistently across the whole novel, so completeness matters more than brevity.

List every named entity, however minor or however often it appears:
- every person, including characters mentioned only once: servants, killers, bandits, family members
- countries, regions, cities, forts, manors, mountains, rivers, passes, buildings, halls
- families and houses as their own entry, even when the name also appears inside a person's full name
- organizations, sects, gangs, bandit groups, guards, military units
- martial-arts techniques, sword styles, inner-power methods, named weapons and items, named books and scriptures, named poisons and medicines
- world-specific titles and ranks, creatures, races, named events, recurring sayings that act as names (e.g. a named code of rules)

Do not skip a name because it is frequent or feels like background. Skip ordinary words, generic job titles (仆人, 管家, 丫环 when not used as a name) and generic honorifics (殿下, 大人, 少爷, 小姐 when not used as a name).

Names hidden in compounds or common words:
- A name that only appears inside a compound still gets its own entry (the X in X家, X堡, X派, 小X).
- A compound that itself names a specific group or institution gets its own entry too.
- A common word that this text uses as the name of one specific entity counts as a name.

Aliases:
- An alias names exactly the same entity: a given name alone, surname + title, a nickname, a title used as a name for one person.
- Never put a different entity in aliases: a person's place, family or organization is a separate entry.
- Do not list the same entity twice.

Gender: be careful, because Arabic grammar depends on it. Decide from the text's pronouns (他/她).

## Verify
After writing the file, verify with a script that it parses and that every term and alias appears verbatim in the window text. Fix or drop any that don't.

Reply with one line only: `<number of terms> ok`, or what failed. Do not paste the glossary.
