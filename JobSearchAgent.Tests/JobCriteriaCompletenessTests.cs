using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class JobCriteriaCompletenessTests
{
    // ReplaceLineEndings("\n") makes the Replace(...) calls below robust to the source file's
    // own line-ending setting (this repo's Windows checkouts have shown LF/CRLF churn on
    // stash/checkout before) — without it, a raw string literal's line terminators follow
    // whatever the file itself uses, and a CRLF file would silently break every "\n"-based
    // Replace below.
    private static readonly string CompleteYaml = """
        target_job_titles: Software Engineer, Backend Developer
        employment_type_preference:
          - full_time
        location:
          countries:
            - Australia
          remote:
            accepted: true
          hybrid:
            accepted: false
          on_site:
            accepted: false
        experience:
          candidate_current: 4 years as a backend engineer
        salary:
          minimum_acceptable: 120000
        skills:
          - C#
          - React
        """.ReplaceLineEndings("\n");

    // TC01 — Every required field present → complete.
    [Fact]
    public void IsComplete_AllFieldsPresent_ReturnsTrue()
    {
        Assert.True(JobCriteriaCompleteness.IsComplete(CompleteYaml));
    }

    // TC02 — Null/empty/whitespace criteria (the auto-seeded blank profile every user starts
    // with) → incomplete, not a throw.
    [Fact]
    public void IsComplete_NullOrEmptyCriteria_ReturnsFalse()
    {
        Assert.False(JobCriteriaCompleteness.IsComplete(null));
        Assert.False(JobCriteriaCompleteness.IsComplete(""));
        Assert.False(JobCriteriaCompleteness.IsComplete("   "));
    }

    // TC03 — This is the exact bug being fixed: only target_job_titles filled in (the old
    // worker gate's entire bar) with nothing else → incomplete under the tightened gate.
    [Fact]
    public void IsComplete_OnlyTargetJobTitles_ReturnsFalse()
    {
        Assert.False(JobCriteriaCompleteness.IsComplete("target_job_titles: Software Engineer"));
    }

    // TC04 — Missing target_job_titles alone (everything else present) → incomplete.
    [Fact]
    public void IsComplete_MissingTargetJobTitles_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("target_job_titles: Software Engineer, Backend Developer\n", "");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC05 — Missing experience.candidate_current → incomplete.
    [Fact]
    public void IsComplete_MissingExperience_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("experience:\n  candidate_current: 4 years as a backend engineer\n", "");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC06 — Empty skills list → incomplete.
    [Fact]
    public void IsComplete_EmptySkills_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("skills:\n  - C#\n  - React", "skills: []");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC07 — Empty employment_type_preference → incomplete.
    [Fact]
    public void IsComplete_EmptyEmploymentTypes_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("employment_type_preference:\n  - full_time\n", "employment_type_preference: []\n");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC08 — No countries under location → incomplete.
    [Fact]
    public void IsComplete_MissingLocationCountries_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("  countries:\n    - Australia\n", "");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC09 — All three work arrangements explicitly declined → incomplete (matches the
    // frontend's "at least one of remote/hybrid/on-site" rule).
    [Fact]
    public void IsComplete_AllArrangementsDeclined_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("remote:\n    accepted: true", "remote:\n    accepted: false");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC10 — location.remote/hybrid/on_site entirely absent from the YAML → still complete,
    // since each defaults to accepted=true (mirrors jobCriteriaYaml.ts's DEFAULTS).
    [Fact]
    public void IsComplete_ArrangementBlockAbsent_DefaultsToAccepted()
    {
        var yaml = """
            target_job_titles: Software Engineer
            employment_type_preference:
              - full_time
            location:
              countries:
                - Australia
            experience:
              candidate_current: 4 years
            salary:
              minimum_acceptable: 120000
            skills:
              - C#
            """;

        Assert.True(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC11 — No salary figure anywhere (minimum_acceptable, target_base, target_max, or
    // thresholds equivalents) → incomplete.
    [Fact]
    public void IsComplete_NoSalaryFigure_ReturnsFalse()
    {
        var yaml = CompleteYaml.Replace("salary:\n  minimum_acceptable: 120000\n", "");
        Assert.NotEqual(CompleteYaml, yaml);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC12 — Salary given only via the current-shape thresholds.target_range (not
    // minimum_acceptable) → still complete.
    [Fact]
    public void IsComplete_SalaryViaThresholdsTargetRange_ReturnsTrue()
    {
        var yaml = CompleteYaml.Replace(
            "salary:\n  minimum_acceptable: 120000\n",
            "salary:\n  thresholds:\n    target_range: [120000, 150000]\n");

        Assert.NotEqual(CompleteYaml, yaml);
        Assert.True(JobCriteriaCompleteness.IsComplete(yaml));
    }

    // TC13 — Malformed/unexpected-shape YAML never throws — degrades to incomplete like
    // JobLocation/TargetJobTitles do for the same reason (user-authored/AI-touched text, not a
    // strict contract).
    [Fact]
    public void IsComplete_MalformedYaml_ReturnsFalseWithoutThrowing()
    {
        var yaml = "target_job_titles: Software Engineer\nlocation: \"just some free text\"";

        var ex = Record.Exception(() => JobCriteriaCompleteness.IsComplete(yaml));

        Assert.Null(ex);
        Assert.False(JobCriteriaCompleteness.IsComplete(yaml));
    }
}
