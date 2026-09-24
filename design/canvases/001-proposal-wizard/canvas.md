---
canvas-id: 001-proposal-wizard
title: Proposal wizard – capture, preview, approval, export
status: draft # draft | in review | approved | superseded
story: "#31"
enriches: ["#32", "#33", "#34", "#35", "#36", "#17"]
epic: "#30"
target-viewport: 1440x900
min-supported-width: 1024
revision: 1
last-updated: 2026-09-24
---

# Proposal wizard – capture, preview, approval, export

## Purpose

The proposal wizard is the single web workspace where sales staff create a commercial document (SOW, RFI, RFP, MSA or Change Request). They talk to the Azure AI Foundry agent, review and correct the generated draft, have it approved, and export it as Word and PDF. This canvas is the binding source for UI behaviour, states and accessibility of that workspace (#31). The UI is German. The document language can be German or English (#36).

## Linked stories

| Story | Title (short) | Covered by |
| ----- | ------------- | ---------- |
| [#31](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/31) | Design canvas for the proposal wizard (this canvas) | whole canvas |
| [#32](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/32) | Choose document type and chat with the agent | states `start-empty`, `capture`, `capture-unclear`, `error-agent` |
| [#17](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/17) | Guided dialog for missing mandatory fields | states `capture`, `capture-unclear`, `summary` |
| [#33](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/33) | Preview and edit before export | states `validation`, `preview`, `export`, `export-done`, `error-export` |
| [#34](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/34) | Human approval before status "final" | states `review`, `denied`, `final`, `reverted` |
| [#35](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/35) | Save drafts and resume them | states `documents`, `documents-empty`, `loading`, `save-failed` |
| [#36](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/36) | Document language DE/EN | state `preview-en`, language control on `start-empty` and in the document bar |
| [#30](https://github.com/NikoMix/IAMCP_rethink2026_usecase/issues/30) | Epic: user workflow and interface | parent epic |

Related, but not enriched here: #20 (`check_plausibility` findings), #28 (file naming, DOCX and PDF from the same data version), #29 (DRAFT watermark, `[GUIDANCE]` markers), #40 ("KI-generiert" labelling), #13 (party names consistent with the MSA).

## Primary user and task

- **User:** Account Manager in sales ("Erstellende Person"). Example user: Lena Vogt (fictional). A second role is the approver ("Freigebende"). Example user: Dr. Jonas Keller, sales management (fictional).
- **Primary task:** Turn a short intent such as „Erstelle ein SOW für Fabrikam, Copilot-Rollout, Festpreis 48.000 EUR“ into a correct, approved SOW exported as DOCX and PDF.
- **Success outcome:** The document shows status „Final“, the approval log names the approver, time and version, and both files download with the same data version and the #28 file name.

## Entry and exit

- **Entry points:** header link „Neues Dokument“ (start page), „Meine Dokumente“ → „Fortsetzen“ (resume a draft), header link „Freigaben“ (approver only), deep link to a document.
- **Exit points:** downloaded DOCX/PDF, „Zur Freigabe senden“ (hand-off to the approver), „Meine Dokumente“.

## Data and permissions

| Item | Source | Required | Notes |
| ---- | ------ | -------- | ----- |
| Document type | user choice or agent inference | yes | `sow`, `rfi`, `rfp`, `msa`, `change-request`; shown as SOW, RFI, RFP, MSA, CR |
| Document data | agent tool calls, inline edits | yes | JSON against `schemas/<doctype>.schema.json`. Field paths such as `project.end_date` come from the field catalog in `templates/README.md` |
| Mandatory field list | template front matter `required_fields` | yes | SOW 15, RFI 9, RFP 11, MSA 12, CR 11. The progress counter uses this list |
| Document language | `document.language` | yes | `de-DE` (default) or `en-US`. Selects the template variant only |
| Plausibility findings | `check_plausibility` tool | yes | `{ findings: [{ code, severity: error\|warning\|info, path, message }] }`. `message` is German (UI language) |
| Status | document record | yes | `draft` → `in_review` → `final`; shown as „Entwurf“, „In Prüfung“, „Final“ |
| Approval log | document record | yes | action, person, timestamp, version. Append-only |
| Chat history | conversation store | yes | persisted with the draft (#35) |
| Current user and roles | Entra ID token | yes | role `Approver` is required to set `final`. Only own documents are listed |
| Supplier (own company) | user profile or configuration | prefilled | e.g. „Contoso Consulting GmbH“ (fictional) |

Permissions: every signed-in user can create, edit, export (with watermark) and submit their own documents. Only the `Approver` role can set `final`. The server enforces this; the UI only reflects it.

## Layout

At 1440 × 900:

1. **App header** (64 px, sticky): brand „Angebotsgenerator“, main navigation („Neues Dokument“, „Meine Dokumente“, „Freigaben“ with a count, approvers only), „Hilfe“, user name and role. A skip link „Zum Inhalt springen“ comes first.
2. **Document bar**: document type as an overline, title „<Kunde> · <Projekt>“ (the page `h1`), status badge, version, document-language select, save indicator (`role="status"`).
3. **Stepper**: 1 „Angaben erfassen“ → 2 „Vorschau und Prüfung“ → 3 „Freigabe“ → 4 „Export“. Completed steps are links; the current step has `aria-current="step"`. A draft export does not advance past step 2.
4. **Workspace**: a two-column grid.
   - **Main column** (flexible, sunken background): either the chat (max 880 px wide, composer sticky at the bottom) or the document preview (a paper-like page, max 820 px).
   - **Side panel** (400 px): context for the current step. In capture it shows mandatory-field progress. In preview it shows „Wert bearbeiten“, „Prüfergebnisse“ and „Nächste Schritte“. In review it shows the approval checklist and actions. In final it shows the approval log.
5. **Modal layer** for export progress and results, and the approval confirmation.

Start page and „Meine Dokumente“ use a single padded content area without the document bar or stepper.

## Components

Names are proposed Blazor components (Razor, .NET 9, interactive server rendering). Only built-in Blazor primitives are assumed (`EditForm`, `InputRadioGroup`, `InputText`, `InputDate`, `InputSelect`, `ValidationMessage`, `AuthorizeView`, `QuickGrid`). No third-party component library is required.

| Component | Purpose | Data | Behavior |
| --------- | ------- | ---- | -------- |
| `AppHeader` | Navigation, identity | user, roles, count of open approvals | „Freigaben“ is rendered only inside `<AuthorizeView Roles="Approver">` |
| `DocumentTypePicker` | Choose a type on the start page | 5 types with a German description | `InputRadioGroup` styled as cards; optional because the agent can infer the type |
| `LanguageSelect` | Choose the document language | `document.language` | radio group on the start page; `InputSelect` in `DocumentBar`. Read-only text while exporting, in review or final |
| `DocumentBar` | Identity and status of the current document | type, customer, project, status, version, save state | hosts `StatusBadge`, `LanguageSelect` and `SaveIndicator` |
| `StatusBadge` | Show the status | `draft` / `in_review` / `final` | text plus a shape icon (○ ◐ ✓) and colour; never colour alone |
| `SaveIndicator` | Autosave feedback | last saved time, failure | `role="status"`; on failure it shows „Nicht gespeichert – neuer Versuch in 10 s“ |
| `WizardStepper` | Orientation | current step | ordered list; completed steps are links |
| `ChatPanel` | Conversation with the agent | messages, streaming tokens | streams via SignalR (`IAsyncEnumerable<string>` from the API). Message list is an `ol`. `aria-busy` is set while streaming |
| `ChatMessage` | One message | author, time, text, AI tag | agent messages carry a visible „KI“ tag (#40) |
| `ChoiceChips` | Agent-offered options (e.g. five document types) | options | a group of buttons with `aria-pressed`; typing a free answer stays possible |
| `ConfirmationSummary` | Summary before generating (#17) | key/value list, findings | the „Bestätigen und Vorschau erstellen“ and „Etwas korrigieren“ actions |
| `ChatComposer` | Message input | text | Enter sends, Shift+Enter adds a line. Send is disabled while the agent is streaming, and the reason is shown as text. „Antwort stoppen“ cancels the stream |
| `RequiredFieldProgress` | Mandatory-field progress | required list, filled values | „x von y Pflichtangaben“ as text, plus `progress` and a grouped field list (✓ erfasst, ○ fehlt) |
| `DocumentPreview` | HTML rendering of the document | document JSON, template, language, status | editable values render as buttons (`FieldValue`). Adds the watermark for non-final status and the „KI-generierter Entwurf“ note |
| `FieldValue` | One editable value in the preview | path, value, severity | button with accessible name „<Label>: <Wert>, bearbeiten“; error ✕ / warning ▲ markers |
| `FieldEditor` | Edit the selected value | path, type, value, findings | `EditForm` with `InputDate`, `InputText`, `InputTextArea`, `InputSelect` or an amount input. `ValidationMessage` shows required-field errors and tool errors. Submit re-runs `check_plausibility` and autosaves |
| `FindingsList` | Show plausibility findings | findings | counts per severity, ordered error → warning → info. Each finding has „Zum Feld“, which selects the field and moves focus into `FieldEditor` |
| `ErrorSummary` | Summary of blocking errors above the preview | error findings | `role="alert"`, focusable (`tabindex="-1"`). Receives focus when an export is blocked. Each item is a link to its field |
| `ExportPanel` / `ExportDialog` | Start the export and show progress and results | data version, formats, file names | modal dialog. DOCX and PDF are produced from one data version. Downloads are offered only when both formats succeed |
| `ApprovalPanel` | Approver's review and decision | checklist, findings, confirmation | rendered only for `Approver`. The confirmation checkbox is required, and a native `<dialog>` confirms the action |
| `ApprovalLog` | Audit trail | entries | ordered list, newest first: action, person, time, version |
| `MyDocumentsTable` | List own documents | documents of the current user | `QuickGrid` with sorting on „Zuletzt geändert“, search, status filter, and „Fortsetzen“ / „Öffnen“ actions |
| `EmptyState` | No data yet | – | explanatory text plus a primary action |
| `InlineAlert` | Status and error messages | severity, text, actions | `role="alert"` for errors, `role="status"` for information |

## Interaction flow

1. **Start (#32, #36).** The user opens „Neues Dokument“. They can pick a type card, type an intent, or both, and pick the document language (default Deutsch). „Erstellung starten“ creates a draft, saves it, and opens the chat. If both type and intent are empty, an error alert appears and receives focus.
2. **Type inference (#17).** If the intent names a type, the agent confirms it („Ich erstelle ein Statement of Work (SOW)“). If not, the agent offers the five types as choice buttons (`capture-unclear`).
3. **Guided capture (#17).** The agent repeats what it took from the intent (customer, project, price model, amount). It pre-fills number, version, date and supplier, and says how many mandatory fields are missing. It then asks **at most three questions per message**. It never re-asks values already given. The side panel updates the „x von y Pflichtangaben“ counter as tool calls fill fields.
4. **Correction (#17).** When the user corrects a value („Der Kunde heißt korrekt Fabrikam GmbH“), the agent updates only that value and says so. The summary marks the changed value „geändert“.
5. **Summary and plausibility (#17, #20).** When all mandatory fields are present, the agent shows a summary card with the `check_plausibility` result. It waits for „Bestätigen und Vorschau erstellen“.
6. **Preview and edit (#33).** The preview renders the document with the draft watermark and the AI note. Every data value is a button. Activating one opens it in `FieldEditor`. „Übernehmen“ saves, re-checks and re-renders. The changed value flashes briefly (not under reduced motion). The result is announced, e.g. „Übernommen: Projektende. Neu geprüft: 0 Fehler, 1 Warnung, 1 Hinweis.“
7. **Validation block (#33, #20).** If error findings exist, „Exportieren“ and „Zur Freigabe senden“ are disabled, and the reason „Export gesperrt: Beheben Sie zuerst 2 Fehler.“ is shown and referenced by `aria-describedby`. The `ErrorSummary` lists each error as a link to its field. Warnings and infos never block.
8. **Export (#33, #28).** The export dialog shows per-format progress. It names both files, e.g. `SOW_Fabrikam-GmbH_SOW-2026-031_v1.0.docx` / `.pdf`, and the data version they are built from. On success it offers both downloads. If either format fails, it offers neither and a retry.
9. **Submit for approval (#34).** „Zur Freigabe senden“ sets the status to „In Prüfung“, logs the submission, and notifies approvers. The creator can keep editing; the status stays „In Prüfung“.
10. **Approve (#34, #29).** The approver opens „Freigaben“ and sees a read-only preview, the prerequisites checklist (all mandatory fields, no errors, no open `[GUIDANCE]` markers, warnings to assess) and the findings. They tick the confirmation and press „Freigeben – Status „Final“ setzen“. A confirmation dialog follows (initial focus on „Abbrechen“). The approval is logged with person, time and version. The watermark disappears.
11. **Permission denied (#34).** A user without the `Approver` role never sees the approve button. If they reach the action anyway (deep link, stale page), the server rejects it. The UI shows „Freigabe nicht möglich …“ and the document is unchanged.
12. **Change after final (#34).** Editing any value of a final document creates version 1.1 and resets the status to „In Prüfung“. The status change is announced. Version 1.0 stays in the log as the final version.
13. **Resume (#35).** „Meine Dokumente“ lists only the user's own documents. „Fortsetzen“ shows a loading skeleton, then restores data, chat history and step.

## States

The six core states required by #31 are marked **(core)**. In the mockup, the „Zustand“ selector and URL hash (e.g. `mockup.html#validation`) switch states. `#core` stacks the six core states; `#all` stacks every state.

| State (mockup key) | Trigger | What the user sees | Available actions |
| ------------------ | ------- | ------------------ | ----------------- |
| **Empty (core)** `start-empty` | first visit, no documents | type cards, intent field, DE/EN choice, „Noch keine Entwürfe“ | pick a type, type an intent, start |
| Start validation (inside `start-empty`) | start with neither type nor intent | `role="alert"` „Wählen Sie einen Dokumenttyp oder beschreiben Sie Ihr Vorhaben.“ with focus | correct and start again |
| **Capture (core)** `capture` | draft started | chat with a streaming agent reply (caret, „Der Agent schreibt …“), progress 12 of 15, grouped field list, „Zur Vorschau“ disabled with its reason | type a reply (Send enabled after streaming), stop the reply, change the language |
| Type unclear `capture-unclear` | intent names no type | five choice buttons plus a free-text hint; panel says the type is not yet set | pick a type, answer freely |
| Summary `summary` | all mandatory fields present, or a correction | the corrected value („geändert“), summary card, findings (0 errors, 1 warning, 1 info) | confirm and preview, correct something, „Zum Feld“ |
| **Validation errors (core)** `validation` | preview or export with error findings | `ErrorSummary` (2 errors) above the preview, ✕ markers on the affected values, editor pre-selected on `project.end_date` with `aria-invalid`, findings list, export and submit disabled with the reason | fix via the editor or „Zum Feld“; export unlocks when the errors reach 0 |
| **Preview (core)** `preview` | summary confirmed | full SOW preview with ENTWURF watermark and the „KI-generierter Entwurf“ note, clickable values, editor hint, findings (1 warning, 1 info) | edit values, export (watermarked), submit for approval, back to the chat |
| Preview English `preview-en` | document language set to en-US | English headings and standard text, en-US dates and amounts, DRAFT watermark; the user's own text stays as entered (marked `lang="de"`) and an info alert says so | edit, switch back to German, export |
| **Export (core)** `export` | „Exportieren“ | modal: DOCX done ✓, PDF in progress (spinner), file names, data version, watermark notice | cancel |
| Export done `export-done` | both formats succeeded | modal with both downloads, sizes and a 24-hour availability hint | download each file, submit for approval, close |
| Export failed `error-export` | PDF conversion failed or timed out | `alertdialog`: no file is offered, data is unchanged, support reference | retry, close |
| **Error (core)** `error-agent` | agent or API unreachable | the unsent user message stays visible, marked „nicht gesendet“, with an inline `role="alert"` and a support reference; progress is unchanged | „Erneut senden“, „Nachricht bearbeiten“ |
| Save failed `save-failed` | autosave failed | red save indicator, alert „Ihre letzte Änderung ist noch nicht gespeichert“ with automatic retry every 10 s, export disabled until saved | „Jetzt speichern“, keep editing |
| Loading `loading` | resuming a draft | skeleton in the chat and the panel, `aria-busy="true"`, text „Entwurf wird geladen: Angaben und Gesprächsverlauf …“ | wait |
| Review (approver) `review` | approver opens a submitted document | read-only preview, prerequisites checklist, findings, confirmation checkbox, approve button, „Mit Kommentar zurückgeben“ | approve (checkbox, then dialog), return with a comment |
| **Permission denied** `denied` | non-approver triggers approval | `role="alert"` „Freigabe nicht möglich“, status „In Prüfung“, what happens next | withdraw the submission, keep editing |
| Final `final` | approval confirmed | success status, no watermark, approval log (who, when, version), export without watermark, „Neue Version bearbeiten“ hint | export, start a new version |
| Reverted `reverted` | data changed after final | warning „Status auf „In Prüfung“ zurückgesetzt“, version 1.1, log entry for the reset, editor on the changed field | edit, submit again |
| My documents `documents` | header link | own documents only: number and title, type, customer, status, language, last changed, action; retention notice; a draft close to deletion shows „wird in 12 Tagen gelöscht“ | search, filter by status, sort, resume, open |
| My documents empty `documents-empty` | no own documents | empty state with explanation | „Neues Dokument erstellen“ |

### Plausibility findings presentation

- The `FindingsList` shows counts for every severity (including zero) and then the findings ordered **error → warning → info**.
- Each finding shows: severity (icon + word: ✕ „Fehler“, ▲ „Warnung“, ℹ „Hinweis“), the tool's `message`, `code · path` in muted small text for support, and „Zum Feld“.
- Values affected by an error or warning are marked in the preview: wavy underline, tinted background, a ✕ or ▲ prefix, and the hidden text „(Fehler)“ / „(Warnung)“. A finding on a collection path, such as `pricing.payment_schedule`, marks every value below it.
- „Zum Feld“ selects the field, scrolls it into view and moves focus to the first input of `FieldEditor`. In read-only contexts (review, final) it moves focus to the value in the preview. From the chat summary it opens the preview with the field selected.
- **Only `error` blocks** export, submission and approval. `warning` must be visible to the approver („1 Warnung – bitte bewerten“). `info` is informational, e.g. `TOTALS_COMPUTED` („Summen und Anteile … hat das Prüfwerkzeug berechnet, nicht das Sprachmodell.“).
- The check runs after every accepted edit and before generating the preview. It shows its time („Geprüft um 11:07 Uhr mit check_plausibility“).
- The codes in the mockup (`DATE_RANGE_INVALID`, `PAYMENT_SUM_MISMATCH`, `MILESTONE_OUTSIDE_PERIOD`, `TOTALS_COMPUTED`) are illustrative. The authoritative codes and messages are owned by #20. The UI renders any code and message the tool returns.

## Interface text

All UI text is German. Only the document content follows the document language (#36). Tone: direct and specific, addressing the user as „Sie“. Messages say what happened and what to do, without blaming the user.

| Element | Text |
| ------- | ---- |
| Start heading / lead | „Neues Dokument erstellen“ / „Wählen Sie einen Dokumenttyp oder beschreiben Sie Ihr Vorhaben. Der Agent fragt nur nach den Angaben, die noch fehlen.“ |
| Intent hint | „Beispiel: „Erstelle ein SOW für Fabrikam, Copilot-Rollout, Festpreis 48.000 EUR“. Geben Sie nur Daten ein, die für das Dokument nötig sind.“ |
| Language hint | „Gilt für die festen Texte der Vorlage. Die Oberfläche bleibt deutsch. Sie können die Sprache später ändern.“ |
| Start button / AI hint | „Erstellung starten“ / „Antworten erzeugt ein KI-Agent. Prüfen Sie alle Inhalte vor der Verwendung.“ |
| Start error | „Wählen Sie einen Dokumenttyp oder beschreiben Sie Ihr Vorhaben.“ |
| Composer | label „Nachricht an den Agenten“; hint „Eingabetaste sendet, Umschalt + Eingabetaste fügt eine neue Zeile ein.“; buttons „Senden“, „Antwort stoppen“ |
| Send disabled reason | „Senden ist möglich, sobald der Agent fertig geantwortet hat.“ |
| Streaming indicator | „Der Agent schreibt …“ |
| Progress | „12 von 15 Pflichtangaben“; list item states „fehlt“, „(vorbelegt)“, „(aus Ihrem Profil)“ |
| Preview gate | „Die Vorschau ist verfügbar, sobald alle Pflichtangaben erfasst sind und Sie die Zusammenfassung bestätigt haben.“ |
| Correction confirmation (agent) | „Aktualisiert: Auftraggeber „Fabrikam“ → „Fabrikam GmbH“. Alle anderen Angaben bleiben unverändert.“ |
| Summary | „Zusammenfassung – bitte prüfen und bestätigen“; buttons „Bestätigen und Vorschau erstellen“, „Etwas korrigieren“ |
| Agent error | „Nachricht nicht gesendet. Der Agent ist gerade nicht erreichbar.“ / „Ihre Nachricht und alle bisherigen Angaben sind gespeichert.“; buttons „Erneut senden“, „Nachricht bearbeiten“; „Referenz für den Support: <id>“ |
| Error summary | „Export nicht möglich: 2 Fehler gefunden“ / „Die Plausibilitätsprüfung hat Angaben gefunden, die sich widersprechen. Korrigieren Sie die Fehler; Warnungen blockieren den Export nicht.“ |
| Export gate reason | „Export gesperrt: Beheben Sie zuerst 2 Fehler.“ / after fixing: „Keine Fehler. Warnungen blockieren den Export nicht; Exporte tragen das Wasserzeichen ENTWURF.“ |
| Editor | heading „Wert bearbeiten“; empty hint „Wählen Sie in der Vorschau einen unterstrichenen Wert, um ihn hier zu bearbeiten. Größere Änderungen beschreiben Sie im Chat.“; buttons „Übernehmen“, „Abbrechen“; required error „Geben Sie einen Wert ein.“; tool error „Fehler: <message>“; „Nach dem Übernehmen wird das Dokument neu geprüft und automatisch gespeichert.“ |
| Findings | heading „Prüfergebnisse“; „Geprüft um <hh:mm> Uhr mit check_plausibility“; severities „Fehler“, „Warnung“, „Hinweis“; link „Zum Feld“; empty „Keine Auffälligkeiten.“ |
| AI note in document | DE „KI-generierter Entwurf – vor Verwendung prüfen“ / EN „AI-generated draft – review before use“ |
| Watermark | DE „ENTWURF“ / EN „DRAFT“ (status „Entwurf“ and „In Prüfung“) |
| Language switched (info) | „Dokumentsprache auf Englisch (en-US) umgestellt. Überschriften, Standardtexte, Datums- und Zahlenformate kommen aus der englischen Vorlage. Ihre eigenen Angaben werden nicht übersetzt.“ |
| Export dialog | „Dokument wird exportiert“; „Beide Dateien entstehen aus Datenstand Version 1.0 vom 24.09.2026, 11:12 Uhr.“; „Export abgeschlossen“; buttons „Word-Datei herunterladen“, „PDF herunterladen“, „Schließen“ |
| Export error | „Export fehlgeschlagen“ / „Das PDF konnte nicht erzeugt werden. Damit Word und PDF denselben Stand zeigen, stellen wir keine der beiden Dateien bereit.“; button „Erneut versuchen“ |
| Save failure | „Nicht gespeichert – neuer Versuch in 10 s“; „Ihre letzte Änderung ist noch nicht gespeichert“; button „Jetzt speichern“ |
| Status | „Entwurf“, „In Prüfung“, „Final“ |
| Approval | „Freigabe“; „Voraussetzungen“; checkbox „Ich habe Inhalt, Preise und Warnungen von Version 1.0 geprüft und gebe das Dokument frei.“; error „Bestätigen Sie die Prüfung, bevor Sie freigeben.“; button „Freigeben – Status „Final“ setzen“; „Mit Kommentar zurückgeben“ |
| Approval dialog | „Version 1.0 final setzen?“; buttons „Abbrechen“, „Final setzen“ |
| Denied | „Freigabe nicht möglich“ / „Den Status „Final“ dürfen nur Personen mit der Rolle „Freigebende“ setzen. Ihr Dokument wurde nicht verändert.“ |
| Final | „Version 1.0 ist final. Freigegeben von <Name> am <Datum> um <Zeit> Uhr.“; heading „Freigabeprotokoll“ |
| Reverted | „Status auf „In Prüfung“ zurückgesetzt. Sie haben „<Feld>“ geändert. Version 1.0 bleibt als finale Fassung im Protokoll; Version 1.1 braucht eine neue Freigabe.“ |
| My documents | „Meine Dokumente“; „4 Dokumente · nur Ihre eigenen“; retention „Entwürfe werden nach 90 Tagen ohne Änderung automatisch gelöscht. Freigegebene Dokumente bleiben erhalten.“; actions „Fortsetzen“, „Öffnen“ |
| My documents empty | „Sie haben noch keine Dokumente“ / „Starten Sie ein Dokument. Es wird automatisch als Entwurf gespeichert, und Sie können es hier später fortsetzen.“ |
| Loading | „Entwurf wird geladen: Angaben und Gesprächsverlauf …“ |

Field labels come from one label map keyed by field path (e.g. `project.end_date` → „Projektende“, `sow.milestones[2].date` → „Meilenstein M3, Datum“). The same labels are used in the progress list, the editor, the error summary and the „Zum Feld“ links.

## Accessibility

Target: WCAG 2.2 level AA.

- **Focus order:** skip link → header navigation → document bar (language select) → stepper links → main column (chat log or preview values in reading order) → composer → side panel (editor → findings → next steps). Modal dialogs trap focus. Everything outside an open dialog is `inert`. Closing a dialog returns focus to the control that opened it.
- **Focus on state change:**
  - Navigating to a new view moves focus to its `h1` (`tabindex="-1"`).
  - A blocked export moves focus to the `ErrorSummary`.
  - Opening a dialog focuses its heading. The approval confirmation focuses „Abbrechen“.
  - „Zum Feld“ focuses the editor input.
  - After „Übernehmen“, focus stays in the editor: on the still-invalid input, or on „Übernehmen“.
  - „Abbrechen“ in the editor returns focus to the value in the preview.
- **Focus visibility:** 3 px outline in `#0550ae` with a 2 px offset on every interactive element, including type cards (`:has(input:focus-visible)`) and preview values. The focused element is never covered by the sticky header or composer (WCAG 2.4.11). Content scrolls with `scroll-margin`.
- **Accessible names and roles:**
  - Landmarks: `header`, `nav` („Hauptnavigation“), `main`, and side panels as `aside` with a label.
  - The chat log is an `ol` („Gesprächsverlauf“). Each message has author and time, and agent messages carry „KI“ as text.
  - Preview values are `button`s named „<Label>: <Wert>, bearbeiten“, plus „(Fehler)“ / „(Warnung)“ when affected. Read-only values are plain text.
  - The progress bar is labelled by its visible text „x von y Pflichtangaben“.
  - Findings: the counts are a labelled list, and each finding states its severity as a word.
  - Status badges contain the status word.
  - Disabled buttons reference their reason with `aria-describedby`, and the reason is visible text.
  - The type cards are one radio group with a `legend`.
  - Tables use `th scope="col"` and a `caption`. The sort state is shown with `aria-sort`.
  - English document content is marked `lang="en"`. The user's own German text inside an English document is marked `lang="de"`.
- **Announcements:**
  - A single polite live region carries results: „Übernommen: <Feld>. Neu geprüft: 0 Fehler, 1 Warnung, 1 Hinweis.“, „Alle Fehler behoben, Export ist jetzt möglich.“, „Der Agent hat geantwortet: …“, and language and status changes.
  - Streaming tokens are **not** announced one by one. The message has `aria-busy="true"` while streaming, and a single summary is announced at the end.
  - Errors use `role="alert"`, and the save indicator uses `role="status"`.
- **Contrast and target size:** measured with the WCAG relative-luminance formula (node script, revision 1):

  | Pair | Ratio | Use |
  | ---- | ----- | --- |
  | `#1f2328` on `#ffffff` / `#f6f8fa` | 15.80 / 14.84 | body text |
  | `#59636e` on `#ffffff` / `#f6f8fa` / `#eef1f4` | 6.11 / 5.74 / 5.39 | muted text, hints |
  | `#ffffff` on `#0969da` | 5.19 | primary buttons |
  | `#0969da` on `#ffffff` / `#ddf4ff` | 5.19 / 4.56 | accent text, info |
  | `#0550ae` on `#ffffff` / `#ddf4ff` | 7.59 / 6.68 | links, focus outline |
  | `#a40e26` on `#ffebe9` / `#ffffff` | 6.86 / 7.87 | error text |
  | `#7d4e00` on `#fff8c5` | 6.58 | warning text |
  | `#116329` on `#dafbe1` | 6.64 | success text |
  | `#1a7f37` on `#ffffff` | 5.08 | success icons |
  | `#6e7781` on `#ffffff` | 4.55 | control borders (≥ 3:1 non-text) |

  Avoid `#d0d7de` (1.45:1) for meaningful borders and `#bf8700` (3.14:1) for text. Severity and status are never shown by colour alone: they always carry an icon shape plus a word. Interactive targets are at least 40 × 40 px (buttons, inputs, choice cards). Inline preview values and text links are exempt under WCAG 2.5.8 (inline in a sentence), and they have 24 px line height.
- **Keyboard-only path:**
  1. On the start page: Tab to the type radio group and choose with the arrow keys, then Tab to the intent field and to the language group, then Enter on „Erstellung starten“.
  2. In the chat: type, then Enter sends.
  3. In the preview: Tab through the values and press Enter to edit, then Enter on „Übernehmen“.
  4. In a finding: Enter on „Zum Feld“.
  5. In the export dialog: Esc closes it (except while files are being produced, where „Abbrechen“ asks the server to cancel).
  6. In the approval dialog: Space toggles the checkbox, Enter approves, and Esc cancels the dialog.
- **Motion:** streaming caret, skeleton shimmer and value flash are disabled under `prefers-reduced-motion: reduce`. There, the agent reply appears in full.
- **Timing:** no time limits on input. The download availability (24 h) is stated in advance, and a new export is always possible.
- **Help (3.2.6):** „Hilfe“ is in the same place in the header on every page.
- **Redundant entry (3.3.7):** the agent never re-asks a value already given, and prefilled values are marked „(vorbelegt)“.

## Responsive behavior

| Width | Behavior |
| ----- | -------- |
| ≥ 1440 (target) | Frame max 1440 px; two columns (main + 400 px panel). Start page: form + 360 px „Zuletzt bearbeitet“ card |
| 1200–1439 | Side panel narrows to 340 px; page padding 32 px |
| 1024–1199 | Single column: side panel stacks below the main column (progress, editor and findings follow the preview). Header wraps and is no longer sticky. Stepper wraps. Chat messages use full width |
| < 1024 | Not a supported target (desktop-first, `min-supported-width: 1024`). Content still reflows without horizontal scrolling down to 320 CSS px (WCAG 1.4.10), except the data tables in the preview and „Meine Dokumente“, which scroll horizontally inside their container |

Long text: titles and values wrap (`overflow-wrap: anywhere`). The progress list truncates long values with „…“ and the full value stays in the preview. File names wrap in monospace.

## Design decisions

| # | Decision | Rationale | Decided by | Date |
| - | -------- | --------- | ---------- | ---- |
| 1 | Chat and structured preview are separate steps of one workspace, not two apps | #32 and #33 are one flow; the stepper keeps orientation, and the panel always shows context | Designer (rev. 1) | 2026-09-24 |
| 2 | Values are edited **inline in the preview** via a side-panel `FieldEditor`, not in a long form | #33 asks for editing before export. Editing next to the rendered text reduces context switches. The editor provides proper labels and validation | Designer | 2026-09-24 |
| 3 | Findings are shown in three places: the side panel list, the markers on values and (errors only) the `ErrorSummary` | the approver needs the whole list, the author needs the location, and a blocked action needs the reason | Designer | 2026-09-24 |
| 4 | Only `error` blocks export, submission and approval; `warning` and `info` never block | matches the `check_plausibility` contract and #20; warnings are assessed explicitly in the approval checklist | Designer | 2026-09-24 |
| 5 | Drafts can be exported, always with the ENTWURF/DRAFT watermark | sales can share drafts early; #29 requires the watermark for non-final status | Designer | 2026-09-24 |
| 6 | If one export format fails, neither file is offered | #28 requires DOCX and PDF from the same data version; offering one would allow divergent copies | Designer (see open decision 4) | 2026-09-24 |
| 7 | The language switch applies immediately and re-renders the preview. There is no confirmation dialog and no machine translation; the user's own text stays as entered and is flagged | #36: template text switches, clauses are not machine-translated; nothing is lost by switching | Designer | 2026-09-24 |
| 8 | Streaming reply with Send disabled until complete, plus „Antwort stoppen“ | avoids interleaved turns; the reason is visible, so the disabled button is not a mystery | Designer | 2026-09-24 |
| 9 | Approve requires a ticked confirmation **and** a confirmation dialog | final is an auditable, status-changing action (#34) | Designer | 2026-09-24 |
| 10 | Changing a final document creates a new minor version and resets the status to „In Prüfung“ | #34 requires the reset; keeping 1.0 in the log preserves the audit trail | Designer | 2026-09-24 |
| 11 | Only Blazor built-in components (`EditForm`, `Input*`, `ValidationMessage`, `AuthorizeView`, `QuickGrid`, native `<dialog>` via JS interop) | keeps the implementation dependency-free and matches the decided stack | Designer | 2026-09-24 |
| 12 | Plausibility messages are shown in German even when the document language is English | the UI language is German (#36 only affects the document) | Designer | 2026-09-24 |

## Open decisions

| # | Question | Options | Recommendation | Owner |
| - | -------- | ------- | -------------- | ----- |
| 1 | Retention period for untouched drafts (#35) | 30 / 90 / 180 days; configurable | **90 days, configurable**, shown in the UI text from configuration. The value in the mockup is an assumption | Product owner + data protection (#41) |
| 2 | May an approver approve their own document? | allow / forbid (four-eyes) | **Forbid**: hide „Freigeben“ for own documents and show „Eigene Dokumente kann eine andere freigebende Person freigeben.“ | Product owner |
| 3 | Preview fidelity | HTML preview from the same Markdown/Scriban output (fast, editable) / rendered PDF image (exact) | **HTML preview**, plus a „PDF-Vorschau“ download in the export dialog for the exact layout | Developer (#24, #33) |
| 4 | If only the PDF fails, offer the DOCX anyway? | withhold both (current) / offer DOCX with a warning | **Withhold both** until #28 confirms whether partial delivery is acceptable | Product owner (#28) |
| 5 | Autosave cadence | on every accepted edit and agent tool call / time-based | **On every accepted edit and tool call**, with a debounced retry every 10 s on failure | Developer (#35) |
| 6 | Language switch after content exists: warn about German user text? | inline info (current) / confirmation dialog | **Inline info** listing the fields with German text | Designer + product owner |
| 7 | Return with comment: visible where for the creator? | banner on the document + entry in the log / chat message | **Banner + log entry** | Product owner (#34) |
| 8 | Deleting documents from „Meine Dokumente“ | not in v1 / delete with confirmation | **Not in v1**, because the retention job covers cleanup. Add it when requested | Product owner (#35) |
| 9 | Field paths `msa.date`, `project.start_date`, `project.end_date`, `pricing.payment_schedule` | align with the schema session (#7) | adopt whatever `schemas/sow.schema.json` defines; the UI label map is keyed by the final path | Data model session (#7) |

## UI acceptance criteria

The criteria are in German to match the stories they will be copied into (#31 AC 3). They become binding when this canvas is approved.

### #32 – Dokumenttyp wählen und mit dem Agenten sprechen

- [ ] Gegeben die Startseite, wenn sie geöffnet wird, dann zeigt sie fünf Dokumenttypen (SOW, RFI, RFP, MSA, CR) als eine Radiogruppe mit Kurzbeschreibung, ein Freitextfeld „Ihr Vorhaben“ und die Dokumentsprache (Standard Deutsch).
- [ ] Gegeben weder Typ noch Vorhaben, wenn „Erstellung starten“ gewählt wird, dann erscheint eine Fehlermeldung mit `role="alert"` und erhält den Fokus.
- [ ] Gegeben eine laufende Agentenantwort, wenn Text gestreamt wird, dann ist „Senden“ deaktiviert, der Grund steht als sichtbarer Text daneben, und „Antwort stoppen“ ist verfügbar.
- [ ] Gegeben eine gestreamte Antwort, wenn sie vollständig ist, dann wird genau eine Zusammenfassung über die Live-Region angesagt (keine Ansage pro Token).
- [ ] Gegeben jede Agentennachricht, wenn sie angezeigt wird, dann trägt sie die sichtbare Kennzeichnung „KI“ (#40).
- [ ] Gegeben der Agent ist nicht erreichbar, wenn eine Nachricht gesendet wird, dann bleibt die Nachricht sichtbar und als „nicht gesendet“ markiert, und „Erneut senden“ sendet sie ohne Neueingabe.
- [ ] Gegeben die Erfassung, wenn Pflichtangaben erfasst werden, dann zeigt das Seitenpanel „x von y Pflichtangaben“ als Text und Fortschrittsbalken sowie je Feld „erfasst“ oder „fehlt“.

### #17 – Geführter Dialog für fehlende Pflichtangaben

- [ ] Gegeben „Erstelle ein SOW für Fabrikam, Copilot-Rollout, Festpreis 48.000 EUR“, wenn der Agent antwortet, dann nennt er die übernommenen Werte, fragt höchstens drei fehlende Angaben pro Nachricht und fragt Kunde, Projekt und Preis nicht erneut.
- [ ] Gegeben ein Vorhaben ohne erkennbaren Typ, wenn der Agent antwortet, dann bietet er die fünf Typen als Schaltflächen an, und eine freie Antwort bleibt möglich.
- [ ] Gegeben eine Korrektur eines Einzelwerts, wenn der Agent sie übernimmt, dann bestätigt er nur diesen Wert, und die Zusammenfassung markiert ihn als „geändert“.
- [ ] Gegeben alle Pflichtangaben, wenn die Zusammenfassung erscheint, dann enthält sie das Ergebnis von `check_plausibility`, und die Vorschau entsteht erst nach „Bestätigen und Vorschau erstellen“.

### #33 – Vorschau und Bearbeitung vor dem Export

- [ ] Gegeben die Vorschau, wenn ein Wert aktiviert wird (Maus, Enter oder Leertaste), dann öffnet „Wert bearbeiten“ mit sichtbarer Beschriftung und Fokus im Eingabefeld.
- [ ] Gegeben eine Änderung, wenn „Übernehmen“ gewählt wird, dann wird `check_plausibility` erneut ausgeführt, die Vorschau aktualisiert, automatisch gespeichert und das Prüfergebnis angesagt.
- [ ] Gegeben Befunde, wenn sie angezeigt werden, dann sind sie nach Fehler → Warnung → Hinweis sortiert, nennen den Schweregrad als Wort und Symbol, und „Zum Feld“ setzt den Fokus auf das zugehörige Eingabefeld.
- [ ] Gegeben mindestens ein Befund mit Schweregrad `error`, wenn exportiert werden soll, dann sind „Exportieren“ und „Zur Freigabe senden“ deaktiviert, der Grund („Export gesperrt: Beheben Sie zuerst n Fehler.“) ist sichtbar und per `aria-describedby` verknüpft, und eine Fehlerzusammenfassung mit Links zu den Feldern erhält den Fokus.
- [ ] Gegeben nur Warnungen und Hinweise, wenn exportiert wird, dann ist der Export möglich.
- [ ] Gegeben Status „Entwurf“ oder „In Prüfung“, wenn die Vorschau oder ein Export angezeigt wird, dann tragen sie das Wasserzeichen ENTWURF (en-US: DRAFT) und den Hinweis „KI-generierter Entwurf“.
- [ ] Gegeben ein Export, wenn er läuft, dann zeigt der Dialog den Fortschritt je Format, beide Dateinamen nach #28 und den gemeinsamen Datenstand. Wenn ein Format fehlschlägt, wird keine Datei angeboten, und „Erneut versuchen“ ist verfügbar.
- [ ] Gegeben ein offener Dialog, wenn mit Tab navigiert wird, dann bleibt der Fokus im Dialog. Nach dem Schließen kehrt er zum auslösenden Element zurück.

### #34 – Menschliche Freigabe vor Status „Final“

- [ ] Gegeben eine Person ohne Rolle „Freigebende“, wenn sie ein Dokument ansieht, dann gibt es keine Schaltfläche „Freigeben“. Wird die Aktion dennoch ausgelöst, dann zeigt die Oberfläche „Freigabe nicht möglich“, und das Dokument bleibt unverändert.
- [ ] Gegeben die Freigabeansicht, wenn sie geöffnet wird, dann zeigt sie eine schreibgeschützte Vorschau, die Voraussetzungen (Pflichtangaben vollständig, keine Fehler, keine offenen `[GUIDANCE]`-Hinweise) und die Anzahl der zu bewertenden Warnungen.
- [ ] Gegeben die Bestätigung ist nicht angehakt, wenn „Freigeben“ gewählt wird, dann erscheint „Bestätigen Sie die Prüfung, bevor Sie freigeben.“, und der Fokus liegt auf der Checkbox.
- [ ] Gegeben die Bestätigung, wenn freigegeben wird, dann fragt ein Dialog nach (Anfangsfokus auf „Abbrechen“), und danach zeigt das Protokoll Person, Zeitpunkt und Version, und das Wasserzeichen entfällt.
- [ ] Gegeben ein finales Dokument, wenn Angaben geändert werden, dann entsteht eine neue Version, der Status wechselt sichtbar und angesagt auf „In Prüfung“, und die finale Version bleibt im Protokoll.

### #35 – Entwürfe speichern und fortsetzen

- [ ] Gegeben „Meine Dokumente“, wenn die Liste geladen ist, dann enthält sie nur eigene Dokumente mit Nummer und Titel, Typ, Kunde, Status (Text), Sprache und „Zuletzt geändert“ (absteigend, `aria-sort`).
- [ ] Gegeben keine eigenen Dokumente, wenn die Liste geöffnet wird, dann erscheint ein leerer Zustand mit „Neues Dokument erstellen“.
- [ ] Gegeben „Fortsetzen“, wenn der Entwurf lädt, dann zeigt ein Skelett mit `aria-busy="true"` und dem Text „Entwurf wird geladen …“ den Ladevorgang, und danach sind Angaben, Gesprächsverlauf und Arbeitsschritt wiederhergestellt.
- [ ] Gegeben ein Speicherfehler, wenn er auftritt, dann zeigt die Speicheranzeige „Nicht gespeichert“, eine Meldung erklärt die automatische Wiederholung, und der Export ist bis zum erfolgreichen Speichern gesperrt.
- [ ] Gegeben die Aufbewahrungsfrist, wenn „Meine Dokumente“ angezeigt wird, dann nennt ein Hinweis die konfigurierte Frist, und Entwürfe kurz vor der Löschung zeigen die verbleibenden Tage.

### #36 – Dokumentsprache DE/EN

- [ ] Gegeben die Sprachauswahl, wenn Englisch gewählt wird, dann bleibt die Oberfläche deutsch, und die Vorschau zeigt englische Überschriften, Standardtexte sowie Datums- und Zahlenformate nach en-US.
- [ ] Gegeben eigene Angaben auf Deutsch, wenn auf Englisch umgestellt wird, dann werden sie nicht übersetzt, ein Hinweis nennt die betroffenen Felder, und sie sind mit `lang="de"` ausgezeichnet.
- [ ] Gegeben Export, Freigabe oder Status „Final“, wenn die Dokumentleiste angezeigt wird, dann ist die Sprache als Text sichtbar und nicht änderbar.

### #31 – Canvas (cross-cutting)

- [ ] Alle Zielgrößen ab 40 × 40 px, Kontraste gemäß Tabelle oben, sichtbarer Fokus auf allen interaktiven Elementen, keine Information allein über Farbe.
- [ ] Bei `prefers-reduced-motion: reduce` gibt es kein Streaming-Caret, keinen Skelett-Schimmer und kein Aufblinken.

## Mockup validation (revision 1)

The following checks ran against `mockup.html` in headless Microsoft Edge. They are not part of CI.

- Every state was rendered via its hash (21 keys: `core`, `all` and 19 states) with a `window.onerror` hook, and 0 script errors were reported. A negative control that injected a `ReferenceError` was reported by the hook.
- A scripted interaction run passed 22 of 22 checks:
  - export gated with 2 errors; fixing the end date leaves 1 error and export stays gated
  - „Zum Feld“ opens the payment-schedule editor with focus; fixing it gives 0 errors, unlocks export and hides the summary
  - an empty required value is rejected
  - start-form error focus
  - approver role switch; approval blocked without the checkbox; confirmation dialog, then final
  - language switch to `preview-en`
  - all 19 selector options map to a section, and 6 core sections exist
  - no duplicate ids, all ARIA id references resolve, all form controls are labelled

  A negative control planted four defects (the gate never unlocks, a duplicate id, a dangling `aria-describedby`, an unlabelled input). All four were reported as FAIL.
- The mockup references no external resources. No `http(s):` or `url(` references and no `src` attributes are present.

## Revision history

| Revision | Date | Change | Driven by |
| -------- | ---- | ------ | --------- |
| 1 | 2026-09-24 | Initial canvas: 19 states including the six core states, the component map for Blazor, accessibility specification with measured contrast, UI acceptance criteria for #32, #17, #33, #34, #35, #36 | #31 |

## Preview

Open [mockup.html](./mockup.html) in a browser. Use the „Zustand“ selector or a hash such as `mockup.html#validation`. `#core` shows the six #31 states stacked; `#all` shows every state. The data is fictional (Contoso Consulting GmbH, Fabrikam GmbH, Northwind Traders AG, Tailspin Toys Ltd.).
