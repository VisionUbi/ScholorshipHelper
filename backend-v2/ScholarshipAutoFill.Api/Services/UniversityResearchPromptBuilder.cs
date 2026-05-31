using ScholarshipAutoFill.Api.Domain;

namespace ScholarshipAutoFill.Api.Services;

public interface IUniversityResearchPromptBuilder
{
    string BuildPrompt(string universityUrl, ApplicantProfile profile);
}

public sealed class UniversityResearchPromptBuilder : IUniversityResearchPromptBuilder
{
    private const string UniversityUrlPlaceholder = "{{PASTE_UNIVERSITY_LINK_HERE}}";
    private const string ApplicantProfilePlaceholder = "{{APPLICANT_PROFILE_HERE}}";

    private const string Template = """
You are an Expert University Admission and Scholarship Research Agent.
Your task is to analyze ONLY the university URL I provide and return the result in EXACT VALID JSON format.
If the URL is a general university/course directory, use official linked pages from that URL to recommend the best matching programs for the applicant profile.
CRITICAL RULES:

Return ONLY JSON.
Do NOT write explanations before or after JSON.
Do NOT use markdown.
Do NOT use code blocks.
Escape all newline characters inside JSON string values.
All text fields must be JSON strings or null.
All date/deadline fields must be JSON strings or null.
Fee amounts, ranges, notes, and living costs must be JSON strings or null, never raw arithmetic expressions.
Boolean fields must be true or false only.
Array fields must be arrays only.
Do NOT put citation numbers or source markers outside JSON strings.
Examples of valid flexible values:
"amount": "10-100"
"annual_fee": "3060"
"monthly": "700-1000"
"notes": "Income-based fee; see official source 6."
Examples of invalid values:
"monthly": 700-1000
"annual_fee": Tuition varies - 6
If information is missing, use null.
Keep the exact JSON structure.
Always extract information directly from the provided university page and official linked pages.
Never change field names.
Never add extra fields.
Always return arrays even if only one item exists.
UNIVERSITY URL:
{{PASTE_UNIVERSITY_LINK_HERE}}
APPLICANT PROFILE:
{{APPLICANT_PROFILE_HERE}}
OUTPUT FORMAT:
{
"university_name": null,
"country": null,
"city": null,
"program_name": null,
"degree_level": null,
"faculty": null,
"study_language": null,
"duration": null,
"intake_year": null,
"application_portal": null,
"application_fee": {
"required": false,
"amount": null,
"currency": null
},
"tuition_fee": {
"annual_fee": null,
"currency": null,
"notes": null
},
"deadlines": [
{
"round": null,
"deadline": null,
"status": null
}
],
"language_requirements": {
"english_required": false,
"minimum_level": null,
"accepted_tests": [
{
"test_name": null,
"minimum_score": null
}
],
"moi_accepted": false,
"moi_details": null
},
"admission_requirements": {
"previous_degree_required": null,
"minimum_gpa": null,
"required_documents": [],
"entry_test_required": false,
"entry_test_name": null
},
"scholarships": [
{
"scholarship_name": null,
"coverage": null,
"amount": null,
"deadline": null,
"eligibility": null
}
],
"estimated_living_cost": {
"monthly": null,
"currency": null
},
"risks": [
{
"risk_type": null,
"details": null,
"severity": "Low"
}
],
"advantages": [],
"official_sources": [],
"overall_assessment": {
"admission_chance": null,
"scholarship_chance": null,
"summary": null
}
}
RISK ANALYSIS RULES:
Always evaluate:

Scholarship competitiveness
Admission competitiveness
English language restrictions
MOI acceptance uncertainty
Visa challenges
Limited seats
High tuition fee
Missing prerequisite courses
Entry test requirements
Portfolio or interview requirements
Severity values allowed:
Low
Medium
High
ADMISSION CHANCE VALUES:
Very High
High
Moderate
Low
Very Low
SCHOLARSHIP CHANCE VALUES:
Very High
High
Moderate
Low
Very Low
Before generating output, verify JSON validity.
Return ONLY VALID JSON.
""";

    public string BuildPrompt(string universityUrl, ApplicantProfile profile)
    {
        if (string.IsNullOrWhiteSpace(universityUrl))
            throw new ArgumentException("University URL is required.", nameof(universityUrl));

        return Template
            .Replace(UniversityUrlPlaceholder, universityUrl.Trim(), StringComparison.Ordinal)
            .Replace(ApplicantProfilePlaceholder, BuildProfileBlock(profile), StringComparison.Ordinal);
    }

    private static string BuildProfileBlock(ApplicantProfile profile)
    {
        return $"""
Full name: {profile.FullName}
Nationality: {profile.Nationality}
Date of birth: {profile.DateOfBirth:yyyy-MM-dd}
Place of birth: {profile.PlaceOfBirth}
Highest degree: {profile.HighestDegree}
University: {profile.University}
CGPA: {profile.Cgpa}
Graduation year: {profile.GraduationYear}
IELTS overall: {profile.IeltsOverall}
IELTS listening: {profile.IeltsListening}
IELTS reading: {profile.IeltsReading}
IELTS writing: {profile.IeltsWriting}
IELTS speaking: {profile.IeltsSpeaking}
CEFR level: {profile.CefrLevel}
Experience: {profile.ExperienceSummary}
Skills: {profile.Skills}
Research interests: {profile.ResearchInterests}
Scholarship preferences: {profile.ScholarshipPreferences}
Preferred regions: {profile.PreferredRegions}
Target degree level: Master's
Recommendation priority: prefer programs aligned with Software Engineering, Computer Science, AI, Data Science, distributed systems, microservices, full-stack development, and scholarship/MOI-friendly admission paths.
""";
    }
}
