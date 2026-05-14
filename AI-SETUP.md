# AI integration (SMS)

This project is **ASP.NET Core MVC** with **Razor** (not a separate React SPA). AI features are integrated via **JSON API controllers** under `/api/ai/...` and a **floating chat widget** in `_Layout.cshtml`.

## Configuration

1. **OpenAI API key (required for chat & narrative formatting)**  
   - Preferred: set environment variable `OPENAI_API_KEY` on the machine or in your hosting profile.  
   - Alternative: set `OpenAI:ApiKey` in `appsettings.Development.json` (do **not** commit secrets to git).

2. **appsettings**  
   - `OpenAI:Model` defaults to `gpt-4o-mini`.  
   - `OpenAI:Enabled` can be set to `false` to disable outbound AI calls (orchestrator will fail fast with a clear message).

3. **Database**  
   After pulling changes, apply the migration (stop `dotnet run` first if the build output is locked):

   ```bash
   dotnet ef database update
   ```

   This creates `AiConversations` and `AiChatMessages` for chat history.

## Architecture (security)

- The model **never** executes raw SQL from the LLM.  
- Flow: **user message → `AiIntentInterpreter` (JSON intent) → `AiSecureDataExecutor` (whitelist EF queries) → `AiResponseFormatter` (natural language from JSON facts only).**  
- **Rate limiting**: fixed window policy `ai` (40 requests / minute / user id or IP).  
- **Role / claim scope**: `AiSecurityContextFactory` resolves Admin, permission claims (`Students.View`, `Fees.View`, `Results.View`), optional **`StudentId` claim** for student-self scope, and **teacher scope** inferred from `TeacherAssignments.TeacherName` matching the signed-in user name or email local-part.

## API endpoints (Admin only; JWT cookie or `Authorization: Bearer`)

| Method | Route | Purpose |
|--------|--------|---------|
| POST | `/api/ai/chat/message` | Body: `{ "conversationId": null, "message": "..." }` |
| GET | `/api/ai/chat/conversations` | List recent conversations for current user |
| GET | `/api/ai/insights/dashboard` | AI insight cards + suggested notifications + optional narrative |
| POST | `/api/ai/reports/export` | Body: `{ "reportType": "fee_defaulters", "format": "xlsx" }` or `executive_summary` + `pdf` |

## UI

- **Floating assistant** (Admins only): `_AiChatAssistant.cshtml` + `wwwroot/js/ai-assistant.js` (uses `localStorage.token` if present for JWT).  
- **Dashboard strip**: `Views/Home/Index.cshtml` loads insights when you open the admin dashboard.

## Limitations / notes

- There is **no daily attendance** entity in this codebase. Intents related to attendance explain the gap; **exam presence** summaries use `StudentResults.Status` counts and are labeled accordingly.  
- **Teacher** matching uses `TeacherAssignment.TeacherName` text match (not a FK to `IdentityUser`). Align names with login for best scoping.  
- **QuestPDF** uses the **Community** license flag for generated PDFs.

## Optional: link a student account

Add a user claim `StudentId` = numeric `StudentID` (via your user-management flow) so non-staff users can query **only their own** data through the assistant.
