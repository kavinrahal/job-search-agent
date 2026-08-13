# Job criteria starter templates

Starter content for the profession categories the multi-user migration targets
beyond Software Engineering (the one real, tuned profile — see
`../job_criteria.yaml`, which drives the owner's actual evaluations).

Each file uses the same generic shape `evaluate_posting.md` and
`PostingEvaluator` expect: `hard_disqualifiers`, `employment_type_preference`,
`location`, `sponsorship`, `experience`, `salary`, `skill_dimensions` (named
per profession — e.g. "Backend stack" for software, "Clinical/functional
specialty" for health care), `company`, `role_type`, `orange_flags`, and
`evaluation_output.recommendation_thresholds`.

These are placeholders, not tuned criteria — every threshold (salary bands,
experience years, skill dimension tiers) is a reasonable starting guess, not
data from a real candidate. Nothing in the app currently loads these files;
they exist for the "Job criteria questionnaire" ticket to use as a starting
point when a new Tier 1 user picks their profession, seeding their
`UserProfile.JobCriteria` from the matching template before they customize it.
