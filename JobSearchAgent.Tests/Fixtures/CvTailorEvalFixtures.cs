namespace JobSearchAgent.Tests;

// Synthetic, fully-fake candidate backgrounds + job postings for CvTailorAgentModelEvalTests.
// Deliberately NOT real user data (see production-safety.md's stance on personal career data,
// which applies even to the operator's own test-account content) -- every name, company,
// employer, achievement, and posting below is invented. Shapes match BackgroundYamlParser's
// snake_case BackgroundData schema exactly, since these are fed straight through
// BackgroundYamlParser.Parse the same way real profile.Background text is.
//
// A plain C# file (raw string literals), not a JSON dataset like
// Fixtures/email_classification_golden.json -- background.yaml is inherently multi-line
// structured text, and this project already embeds YAML/markdown fixtures the same way
// (CvTailorAgentTests.ContactBackgroundYaml, ContractTests.SamplePosting) rather than escaping
// them into a JSON string value.
//
// 10 fixtures, spanning resume length/complexity on purpose (short, long-and-dense, single very
// long tenure, sparse/early-career) plus the "unusual section" and formatting edge cases called
// out in the CvTailorAgent/CoverLetterAgent model-eval task: credentials, publications,
// volunteering, an employment gap, and non-ASCII personal details.
public record CvTailorFixture(string Id, string Description, string BackgroundYaml, string PostingText, string EvaluationJson);

public static class CvTailorEvalFixtures
{
    public static readonly IReadOnlyList<CvTailorFixture> All =
    [
        new(
            Id: "short_2_role",
            Description: "Short resume, 2 roles -- baseline / lowest-risk case.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Priya Nair
                  email: priya.nair@example.com
                  location: Brisbane, QLD
                experience:
                  - company: Fernwood Digital
                    role: Backend Engineer
                    dates:
                      start: "2023-02"
                    location: Brisbane, QLD
                    employment_type: full_time
                    domain: e-commerce
                    achievements:
                      - Built and shipped a REST API in C# and ASP.NET Core handling checkout for an online homewares store.
                      - Reduced average checkout API latency from 800ms to 220ms by adding Redis caching for product lookups.
                      - Wrote integration tests with xUnit, raising backend test coverage from 30% to 75%.
                  - company: CampusHub
                    role: Junior Developer
                    dates:
                      start: "2021-11"
                      end: "2023-01"
                    location: Brisbane, QLD
                    employment_type: full_time
                    domain: education technology
                    achievements:
                      - Maintained a Django-based student timetabling tool used by 4 universities.
                      - Fixed a recurring timezone bug that caused incorrect exam schedules for interstate students.
                education:
                  - institution: University of Queensland
                    degree: Bachelor of Computer Science
                    location: Brisbane, QLD
                    graduation_year: 2021
                projects: []
                """,
            PostingText: """
                Company: Northlake Payments
                Role: Backend Engineer
                Location: Brisbane, QLD (hybrid)
                Salary: $95,000 - $115,000 AUD
                Experience: 2+ years C# or similar backend language
                Description: Build and maintain APIs for our merchant payments platform.
                Stack: C#, ASP.NET Core, PostgreSQL, Redis, Docker.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","backend_match":"strong","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[{"dimension":"Backend stack","match":"strong","detail":"C#, ASP.NET Core, Redis, PostgreSQL"}],
                 "rationale":"Direct stack match, payments domain adjacent to checkout work."}
                """),

        new(
            Id: "long_6_role_dense",
            Description: "Long resume, 6 roles with dense 5-6 bullet achievement lists -- highest-risk shape for the Experience call's token budget.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Marcus Chen
                  email: marcus.chen@example.com
                  phone: "0412 555 123"
                  location: Melbourne, VIC
                  linkedin: linkedin.com/in/marcuschen
                  github: github.com/marcuschen
                experience:
                  - company: Ridgeline Cloud
                    role: Staff Engineer
                    dates:
                      start: "2024-01"
                    location: Melbourne, VIC
                    employment_type: full_time
                    domain: cloud infrastructure
                    company_description: Series C infrastructure-as-a-service startup, ~200 employees.
                    achievements:
                      - Led the migration of a monolithic .NET Framework 4.8 application to .NET 8 microservices across 5 teams.
                      - Designed a multi-region failover architecture on Azure reducing platform downtime from 4 hours/quarter to under 15 minutes/quarter.
                      - Introduced an internal Kubernetes operator that automated 90% of routine cluster scaling decisions.
                      - Mentored 6 mid-level engineers through a formal staff-track mentoring program.
                      - Chaired the architecture review board, approving or redirecting 40+ design proposals over 18 months.
                  - company: Orbital Freight Systems
                    role: Senior Software Engineer
                    dates:
                      start: "2021-06"
                      end: "2023-12"
                    location: Melbourne, VIC
                    employment_type: full_time
                    domain: logistics
                    achievements:
                      - Rebuilt the shipment-tracking service in C# and gRPC, cutting P99 latency from 1.2s to 180ms.
                      - Owned the on-call rotation for a system processing 2 million tracking events per day.
                      - Introduced contract testing between 12 microservices, catching breaking changes before release for the first time.
                      - Built a cost-anomaly detector on Azure spend that flagged $40,000/year in unused reserved capacity.
                  - company: Beacon Analytics
                    role: Software Engineer II
                    dates:
                      start: "2019-08"
                      end: "2021-05"
                    location: Melbourne, VIC
                    employment_type: full_time
                    domain: data analytics
                    achievements:
                      - Built ETL pipelines in Python and Airflow ingesting data from 15 external partner APIs.
                      - Designed a schema-versioning strategy for a multi-tenant Postgres warehouse.
                      - Reduced nightly batch job runtime from 6 hours to 90 minutes through parallelization.
                  - company: Foothill Systems
                    role: Software Engineer
                    dates:
                      start: "2017-07"
                      end: "2019-07"
                    location: Adelaide, SA
                    employment_type: full_time
                    domain: enterprise software
                    achievements:
                      - Developed internal HR tooling in C# WinForms for a 3,000-employee manufacturing company.
                      - Automated a manual payroll reconciliation process, saving the finance team roughly 10 hours/week.
                      - Wrote the team's first suite of automated UI tests using Selenium.
                  - company: Epic Lanka
                    role: UI/UX Intern
                    dates:
                      start: "2017-01"
                      end: "2017-06"
                    location: Colombo, Sri Lanka (remote)
                    employment_type: internship
                    domain: design
                    achievements:
                      - Designed wireframes and prototypes in Figma for a small SaaS dashboard product.
                      - Ran 8 user interviews and synthesized findings into a redesigned onboarding flow.
                  - company: Programmed
                    role: Freelance Software Contractor
                    dates:
                      start: "2016-03"
                      end: "2016-12"
                    location: Adelaide, SA
                    employment_type: contract
                    domain: legacy modernization
                    achievements:
                      - Maintained an ASP.NET WebForms line-of-business application for a regional retailer under a fixed-price contract.
                      - Patched a critical SQL injection vulnerability in the legacy checkout module.
                education:
                  - institution: University of Adelaide
                    degree: Bachelor of Software Engineering
                    location: Adelaide, SA
                    graduation_year: 2016
                projects:
                  - name: routewise
                    status: active
                    description: Open-source route optimization library for last-mile delivery.
                    highlights:
                      - Implemented a constrained vehicle routing solver in Rust with Python bindings.
                      - Adopted by two small logistics startups as a dependency.
                """,
            PostingText: """
                Company: Vantage Cloud Partners
                Role: Staff Software Engineer, Platform
                Location: Melbourne, VIC (hybrid)
                Salary: $190,000 - $220,000 AUD
                Experience: 8+ years, prior staff/lead experience preferred
                Description: Own platform reliability and architecture for a multi-region cloud infrastructure product. Heavy mentoring and cross-team design review component.
                Stack: C#, .NET 8, Kubernetes, Azure, gRPC.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","backend_match":"strong","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[
                   {"dimension":"Backend stack","match":"strong","detail":".NET 8, gRPC, Kubernetes, Azure"},
                   {"dimension":"Leadership scope","match":"strong","detail":"staff-level architecture review and mentoring"}],
                 "rationale":"Near-exact match on staff-level infra scope, architecture ownership, and mentoring."}
                """),

        new(
            Id: "unusual_sections_credentials_pubs_volunteering",
            Description: "Healthcare professional pivoting to health-tech -- exercises Credentials, Publications, and Volunteering sections together.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Aisha Rahman
                  email: aisha.rahman@example.com
                  location: Sydney, NSW
                experience:
                  - company: St. Aldwyn Hospital
                    role: Clinical Informatics Nurse
                    dates:
                      start: "2022-03"
                    location: Sydney, NSW
                    employment_type: full_time
                    domain: healthcare
                    achievements:
                      - Led the ward-level rollout of a new electronic medication administration record (eMAR) system across 6 wards.
                      - Reduced medication charting errors by 35% through a redesigned nurse-facing workflow.
                      - Trained 120 nursing staff on the new clinical documentation system.
                  - company: St. Aldwyn Hospital
                    role: Registered Nurse
                    dates:
                      start: "2018-02"
                      end: "2022-02"
                    location: Sydney, NSW
                    employment_type: full_time
                    domain: healthcare
                    achievements:
                      - Provided direct patient care on a 30-bed general medicine ward.
                      - Served as a super-user for the hospital's transition from paper to electronic charting.
                education:
                  - institution: University of Sydney
                    degree: Bachelor of Nursing
                    location: Sydney, NSW
                    graduation_year: 2017
                projects: []
                credentials:
                  - kind: license
                    name: Registered Nurse
                    issuer: Nursing and Midwifery Board of Australia
                    id_or_number: "NMW00219384"
                    issued_date: "2018-01"
                    status: active
                publications:
                  - title: Reducing Medication Charting Errors Through Workflow-Aware EMAR Design
                    venue: Australian Journal of Clinical Informatics
                    date: "2023-11"
                    authors: A. Rahman, T. Nguyen
                volunteering:
                  - role: Digital Health Literacy Volunteer
                    org: Sydney Community Health Alliance
                    dates:
                      start: "2020-01"
                    description: Runs monthly workshops helping elderly patients use patient-portal apps.
                """,
            PostingText: """
                Company: Meridian Health Systems
                Role: Clinical Product Specialist
                Location: Sydney, NSW (hybrid)
                Salary: $110,000 - $125,000 AUD
                Experience: Clinical background required, health IT/EMR experience highly valued
                Description: Bridge clinical workflows and product development for our hospital EMR platform. Work directly with nursing staff and engineering.
                Stack: n/a (clinical + product role), familiarity with EMR systems expected.
                """,
            EvaluationJson: """
                {"recommendation":"good_match","company_assessment":"acceptable","role_type_match":"preferred",
                 "skill_matches":[
                   {"dimension":"Clinical background","match":"strong","detail":"Registered Nurse, active license"},
                   {"dimension":"EMR/health IT experience","match":"strong","detail":"eMAR rollout, clinical informatics"}],
                 "rationale":"Strong clinical-to-product bridge candidate given informatics nursing role and EMR rollout leadership."}
                """),

        new(
            Id: "recent_grad_sparse",
            Description: "Recent graduate, one internship and one junior role, minimal content -- tests behavior on a thin BACKGROUND rather than a dense one.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Ellie Sutherland
                  email: ellie.sutherland@example.com
                  location: Perth, WA
                experience:
                  - company: Harbour Analytics
                    role: Junior Data Analyst
                    dates:
                      start: "2025-07"
                    location: Perth, WA
                    employment_type: full_time
                    domain: analytics
                    achievements:
                      - Built weekly sales dashboards in Power BI for the regional retail team.
                      - Wrote SQL queries to clean and join data from 3 internal systems.
                  - company: Harbour Analytics
                    role: Data Intern
                    dates:
                      start: "2025-01"
                      end: "2025-06"
                    location: Perth, WA
                    employment_type: internship
                    domain: analytics
                    achievements:
                      - Assisted senior analysts with data cleaning in Python and pandas.
                education:
                  - institution: Curtin University
                    degree: Bachelor of Commerce, Business Analytics
                    location: Perth, WA
                    graduation_year: 2025
                projects:
                  - name: uni-timetable-optimizer
                    status: side_project
                    description: A small Python script that suggests non-clashing subject enrolments.
                    highlights:
                      - Used a simple constraint-satisfaction approach in Python.
                """,
            PostingText: """
                Company: Coastline Retail Group
                Role: Data Analyst
                Location: Perth, WA
                Salary: $70,000 - $80,000 AUD
                Experience: 0-2 years
                Description: Support the merchandising team with sales and inventory reporting.
                Stack: SQL, Power BI, Python.
                """,
            EvaluationJson: """
                {"recommendation":"good_match","company_assessment":"acceptable","role_type_match":"preferred",
                 "skill_matches":[{"dimension":"Analytics stack","match":"good","detail":"SQL, Power BI, Python"}],
                 "rationale":"Entry-level analytics role matches current experience level and stack closely."}
                """),

        new(
            Id: "career_changer_marketing_to_data",
            Description: "Career changer, marketing -> data analytics, roles span two different domains -- tests tailoring judgment across a discontinuous history.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Daniel Okafor
                  email: daniel.okafor@example.com
                  location: Adelaide, SA
                experience:
                  - company: Bright Path Analytics Bootcamp
                    role: Data Analytics Trainee
                    dates:
                      start: "2025-03"
                      end: "2025-08"
                    location: Adelaide, SA (remote)
                    employment_type: full_time
                    domain: analytics training
                    achievements:
                      - Completed a 20-week intensive covering SQL, Python, and Tableau, including a capstone project.
                      - Built a capstone dashboard analyzing 3 years of public transport ridership data for a mock city council client.
                  - company: Silvermark Marketing Agency
                    role: Marketing Analyst
                    dates:
                      start: "2021-05"
                      end: "2025-02"
                    location: Adelaide, SA
                    employment_type: full_time
                    domain: marketing
                    achievements:
                      - Analyzed campaign performance across Google Ads and Meta Ads for 12 client accounts.
                      - Built recurring Excel and Tableau reports summarizing spend, conversion rate, and ROAS for clients.
                      - Identified a targeting change that improved one client's cost-per-lead by 22%.
                  - company: Silvermark Marketing Agency
                    role: Marketing Coordinator
                    dates:
                      start: "2019-06"
                      end: "2021-04"
                    location: Adelaide, SA
                    employment_type: full_time
                    domain: marketing
                    achievements:
                      - Coordinated social media scheduling and reporting for 6 small-business clients.
                education:
                  - institution: Flinders University
                    degree: Bachelor of Marketing
                    location: Adelaide, SA
                    graduation_year: 2019
                projects: []
                """,
            PostingText: """
                Company: Parklands Council
                Role: Junior Data Analyst
                Location: Adelaide, SA
                Salary: $75,000 - $85,000 AUD
                Experience: 0-2 years in a data/analytics role, career changers welcome
                Description: Support council reporting on public services usage with SQL and Tableau dashboards.
                Stack: SQL, Tableau, Excel.
                """,
            EvaluationJson: """
                {"recommendation":"good_match","company_assessment":"acceptable","role_type_match":"acceptable",
                 "skill_matches":[
                   {"dimension":"Analytics stack","match":"good","detail":"SQL, Tableau"},
                   {"dimension":"Domain relevance","match":"acceptable","detail":"public transport ridership capstone project"}],
                 "rationale":"Career changer with directly relevant capstone project and transferable reporting experience from marketing analytics."}
                """),

        new(
            Id: "dense_projects_no_volunteering",
            Description: "Engineer with a heavy Projects section (4 open-source projects) and 3 roles, no volunteering/credentials/publications at all.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Yusuf Demir
                  email: yusuf.demir@example.com
                  github: github.com/yusufdemir
                  location: Hobart, TAS
                experience:
                  - company: Tarn Software
                    role: Software Engineer
                    dates:
                      start: "2022-09"
                    location: Hobart, TAS (remote)
                    employment_type: full_time
                    domain: developer tools
                    achievements:
                      - Built a CLI tool in Go for managing multi-cloud Terraform state, adopted internally by 4 teams.
                      - Reduced CI pipeline runtime by 40% by parallelizing the test suite.
                  - company: Basalt Systems
                    role: Software Engineer
                    dates:
                      start: "2020-07"
                      end: "2022-08"
                    location: Hobart, TAS
                    employment_type: full_time
                    domain: fintech
                    achievements:
                      - Implemented a reconciliation service in Go processing daily bank settlement files.
                      - Wrote a Go linter plugin enforcing internal error-handling conventions.
                  - company: Basalt Systems
                    role: Graduate Software Engineer
                    dates:
                      start: "2019-02"
                      end: "2020-06"
                    location: Hobart, TAS
                    employment_type: full_time
                    domain: fintech
                    achievements:
                      - Rotated across 3 backend teams during an 18-month graduate program.
                education:
                  - institution: University of Tasmania
                    degree: Bachelor of Information Technology
                    location: Hobart, TAS
                    graduation_year: 2019
                projects:
                  - name: tfstate-guard
                    status: active
                    description: Open-source Go CLI for locking and auditing Terraform state changes across teams.
                    highlights:
                      - 800+ GitHub stars.
                      - Used in production by two mid-size infrastructure teams outside the author's employer.
                  - name: goenv-lint
                    status: active
                    description: A Go linter that flags missing environment-variable validation at startup.
                    highlights:
                      - Integrated into Basalt Systems' internal CI pipeline.
                  - name: hobart-transit-api
                    status: archived
                    description: Unofficial wrapper around Hobart's public transit data feed.
                    highlights:
                      - Reverse-engineered an undocumented GTFS-realtime feed.
                  - name: dotfiles
                    status: active
                    description: Personal shell and editor configuration repository.
                    highlights:
                      - Not directly relevant to most roles.
                """,
            PostingText: """
                Company: Iron Peak Infrastructure
                Role: Platform Engineer
                Location: Hobart, TAS (remote)
                Salary: $135,000 - $155,000 AUD
                Experience: 4+ years, strong Go and infrastructure-as-code background
                Description: Build internal developer tooling for a platform team supporting 30 engineers.
                Stack: Go, Terraform, CI/CD, GitHub Actions.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","backend_match":"strong","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[{"dimension":"Infra/tooling stack","match":"strong","detail":"Go, Terraform, CI/CD"}],
                 "rationale":"Direct match on Go + Terraform developer-tooling experience, including a widely-used open-source Terraform tool."}
                """),

        new(
            Id: "long_tenure_single_role",
            Description: "One very long single-role tenure (10 years) with a large achievement list -- stresses per-role judgment within the Experience call on a single dense entry rather than many roles.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Robert Klein
                  email: robert.klein@example.com
                  location: Canberra, ACT
                experience:
                  - company: Federal Systems Group
                    role: Senior DevOps Engineer
                    dates:
                      start: "2015-04"
                    location: Canberra, ACT
                    employment_type: full_time
                    domain: government IT
                    company_description: Long-running government systems integrator, ~800 employees.
                    achievements:
                      - Migrated 40+ legacy applications from on-premises VMware to AWS GovCloud over a 3-year program.
                      - Built the organization's first CI/CD pipeline standard using Jenkins and later GitHub Actions, adopted by 15 teams.
                      - Reduced average deployment time from 2 days (manual, ticket-based) to under 30 minutes.
                      - Introduced infrastructure-as-code with Terraform, replacing manually-provisioned environments across the department.
                      - Led incident response for a production outage affecting a citizen-facing benefits portal, restoring service in 45 minutes.
                      - Designed the department's disaster recovery strategy, achieving a documented RTO of under 4 hours.
                      - Mentored a rotating group of 3-4 junior engineers per year through the department's graduate program.
                      - Authored internal security hardening standards later adopted department-wide after an external audit.
                      - Managed vendor relationships for 3 major cloud and monitoring tooling contracts.
                      - Presented the CI/CD modernization program to department leadership, securing continued funding for 2 further years.
                education:
                  - institution: Australian National University
                    degree: Bachelor of Information Technology
                    location: Canberra, ACT
                    graduation_year: 2014
                projects: []
                """,
            PostingText: """
                Company: Southbank Digital Services
                Role: Lead DevOps Engineer
                Location: Canberra, ACT (hybrid)
                Salary: $170,000 - $195,000 AUD
                Experience: 8+ years DevOps/infrastructure, government sector experience a plus
                Description: Lead infrastructure modernization for a government-adjacent digital services provider, including CI/CD, cloud migration, and disaster recovery planning.
                Stack: AWS, Terraform, GitHub Actions, Kubernetes.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","backend_match":"strong","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[
                   {"dimension":"Infra/cloud stack","match":"strong","detail":"AWS, Terraform, GitHub Actions"},
                   {"dimension":"Leadership scope","match":"strong","detail":"led multi-year CI/CD and cloud migration program"}],
                 "rationale":"Near-identical scope: government-adjacent infra modernization, CI/CD leadership, disaster recovery planning."}
                """),

        new(
            Id: "unicode_international_formatting",
            Description: "Non-ASCII personal details and locations (accented characters, umlauts) -- tests robustness of the tailoring/rendering path on non-English-typical text, not just content quality.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: José García Muñoz
                  email: jose.garcia@example.com
                  location: München, Germany
                  linkedin: linkedin.com/in/josegarciamunoz
                experience:
                  - company: Nordlicht Software GmbH
                    role: Backend-Entwickler (Backend Developer)
                    dates:
                      start: "2021-09"
                    location: München, Germany
                    employment_type: full_time
                    domain: logistics software
                    company_description: Mittelständisches Unternehmen (mid-sized company) building fleet management software.
                    achievements:
                      - Entwickelte eine REST-API in C# und .NET 8 für die Flottenverfolgung in Echtzeit (Developed a real-time fleet tracking REST API in C# and .NET 8).
                      - Reduzierte die durchschnittliche Antwortzeit der API um 45% durch gezielte Datenbankoptimierung (Reduced average API response time by 45% through targeted database optimization).
                  - company: Alpenblick Consulting
                    role: Software-Entwickler (Software Developer)
                    dates:
                      start: "2018-10"
                      end: "2021-08"
                    location: Innsbruck, Österreich
                    employment_type: full_time
                    domain: consulting
                    achievements:
                      - Baute interne Tools für Kundenprojekte in C# und SQL Server (Built internal tools for client projects in C# and SQL Server).
                education:
                  - institution: Technische Universität München
                    degree: Bachelor of Science, Informatik
                    location: München, Germany
                    graduation_year: 2018
                projects: []
                """,
            PostingText: """
                Company: Alpenroute Logistik AG
                Role: Backend Engineer
                Location: München, Germany (hybrid)
                Salary: €70,000 - €85,000
                Experience: 3+ years C# / .NET
                Description: Build backend services for real-time fleet tracking across the DACH region.
                Stack: C#, .NET 8, SQL Server, Azure.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","backend_match":"strong","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[{"dimension":"Backend stack","match":"strong","detail":"C#, .NET 8, SQL Server, real-time tracking"}],
                 "rationale":"Direct domain and stack match, existing real-time fleet tracking experience in the same region."}
                """),

        new(
            Id: "employment_gap_notes",
            Description: "Resume with an 18-month employment gap and a Notes field explaining it -- tests that tailoring doesn't try to paper over or fabricate content around the gap.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Grace Thompson
                  email: grace.thompson@example.com
                  location: Auckland, New Zealand
                experience:
                  - company: Kauri Digital
                    role: Product Designer
                    dates:
                      start: "2024-02"
                    location: Auckland, New Zealand
                    employment_type: full_time
                    domain: SaaS
                    achievements:
                      - Redesigned the onboarding flow for a B2B SaaS product, reducing time-to-first-value from 12 minutes to 4 minutes.
                      - Ran a design system consolidation project unifying 3 inconsistent component libraries into one.
                  - company: Freelance
                    role: Freelance Product Designer
                    dates:
                      start: "2022-08"
                      end: "2024-01"
                    location: Auckland, New Zealand (remote)
                    employment_type: contract
                    domain: various
                    notes: Deliberately reduced workload during this period for a family caregiving commitment; took select part-time freelance work rather than a full-time role.
                    achievements:
                      - Delivered UX audits and redesign recommendations for 4 small-business clients on a part-time contract basis.
                  - company: Harbourline Studio
                    role: UX Designer
                    dates:
                      start: "2019-11"
                      end: "2022-07"
                    location: Auckland, New Zealand
                    employment_type: full_time
                    domain: digital agency
                    achievements:
                      - Led UX design for 6 client web application projects across retail and healthcare sectors.
                      - Introduced regular usability testing into the studio's process, previously ad hoc.
                education:
                  - institution: Auckland University of Technology
                    degree: Bachelor of Design
                    location: Auckland, New Zealand
                    graduation_year: 2019
                projects: []
                """,
            PostingText: """
                Company: Tidewater Software
                Role: Senior Product Designer
                Location: Auckland, New Zealand (hybrid)
                Salary: NZD $105,000 - $125,000
                Experience: 4+ years product design
                Description: Own end-to-end design for our core SaaS product, including onboarding and design system work.
                Stack: Figma, design systems, usability testing.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[{"dimension":"Product design experience","match":"strong","detail":"onboarding redesign, design system consolidation"}],
                 "rationale":"Direct match on onboarding UX and design system work, recent and relevant."}
                """),

        new(
            Id: "legal_bar_credential_pivot",
            Description: "Lawyer pivoting to legal-tech -- bar admission credential plus publications, 2 roles, tests a professional domain far from software engineering.",
            BackgroundYaml: """
                version: "1"
                personal:
                  name: Sarah Whitfield
                  email: sarah.whitfield@example.com
                  location: Sydney, NSW
                experience:
                  - company: Ashcombe & Reyes Lawyers
                    role: Senior Associate, Commercial Law
                    dates:
                      start: "2020-03"
                    location: Sydney, NSW
                    employment_type: full_time
                    domain: commercial law
                    achievements:
                      - Led contract review and negotiation for technology-sector clients, including 8 SaaS vendor agreements per year on average.
                      - Built the firm's first standardized contract-review checklist for technology transactions, adopted firm-wide.
                      - Advised a mid-size client through a data-processing agreement dispute involving GDPR and Australian Privacy Act obligations.
                  - company: Ashcombe & Reyes Lawyers
                    role: Associate
                    dates:
                      start: "2017-02"
                      end: "2020-02"
                    location: Sydney, NSW
                    employment_type: full_time
                    domain: commercial law
                    achievements:
                      - Drafted and reviewed commercial contracts under partner supervision across a range of industries.
                education:
                  - institution: University of New South Wales
                    degree: Bachelor of Laws (LLB)
                    location: Sydney, NSW
                    graduation_year: 2016
                projects: []
                credentials:
                  - kind: bar_admission
                    name: Admitted Solicitor
                    issuer: Law Society of New South Wales
                    id_or_number: "NSW-84421"
                    issued_date: "2017-01"
                    status: active
                publications:
                  - title: Contract Review Automation and the Limits of Standardization in SaaS Procurement
                    venue: NSW Law Society Journal
                    date: "2024-06"
                    authors: S. Whitfield
                """,
            PostingText: """
                Company: Clauseworks
                Role: Legal Product Specialist
                Location: Sydney, NSW (hybrid)
                Salary: $130,000 - $150,000 AUD
                Experience: Qualified lawyer, commercial/technology contracts background preferred
                Description: Shape contract-review automation product features by bridging legal domain expertise and the engineering team.
                Stack: n/a (legal + product role), interest in legal-tech required.
                """,
            EvaluationJson: """
                {"recommendation":"strong_match","company_assessment":"preferred","role_type_match":"preferred",
                 "skill_matches":[
                   {"dimension":"Legal domain expertise","match":"strong","detail":"commercial/technology contract review, SaaS vendor agreements"},
                   {"dimension":"Legal-tech interest/evidence","match":"strong","detail":"published on contract review automation"}],
                 "rationale":"Directly relevant contract-review domain expertise plus a publication specifically on contract automation."}
                """),
    ];
}
