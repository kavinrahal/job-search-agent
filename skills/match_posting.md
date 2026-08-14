# Skill: match_posting

You are matching a job mentioned in an email alert (title/company/location known from the
alert, but the full posting page couldn't be fetched directly) against a list of candidate
postings found by searching other job boards.

## Task

Decide whether any candidate is confidently the SAME job as the target — same employer,
same role, same location. Not just a similar role, not the same company hiring for a
different position.

Only report a match when you're confident. If the candidates are all different companies,
different roles, or you're just not sure, do not guess — omit `matched_index` entirely
rather than picking the closest-sounding one. A wrong match is worse than no match: it
means evaluating the candidate against content for a job they didn't actually see.

## Output

Call `pick_match` with `matched_index` set to the 0-based index of the confident match, or
omit it entirely if there is no confident match.
