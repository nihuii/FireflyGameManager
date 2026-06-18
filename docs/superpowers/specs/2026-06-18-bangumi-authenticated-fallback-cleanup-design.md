# Bangumi Authenticated Fallback and Repository Cleanup Design

## Goal

Implement solution A from `问题.md`: use a valid Bangumi account for subject details, recover from detail `404` responses through legacy `responseGroup=large` search data, make detail refresh self-healing, distinguish authentication/partial/not-found outcomes, and remove obsolete development artifacts without deleting maintained regression tests or project history.

## Considered Approaches

1. **Provider-level orchestration (selected).** Keep HTTP concerns in `BangumiApiClient`, then let `BangumiGameMetadataProvider` combine account state, authenticated detail lookup, legacy fallback, and a typed lookup status. Both add/edit and detail views consume the same behavior.
2. **ViewModel-level orchestration.** Let each view call details, account storage, and search independently. This avoids a new result type but duplicates the most failure-prone logic and allows the two screens to disagree.
3. **Transparent API-client fallback.** Hide all fallback inside `BangumiApiClient` and continue returning only metadata or null. This is compact, but loses the information needed to distinguish complete data, partial data, expired credentials, and a truly deleted subject.

## Architecture

- Add a typed metadata lookup result with four states: complete, partial, account reconnect required, and not found.
- Extend `IBangumiApiClient` compatibly with authenticated detail lookup and explicit legacy large-result search. Existing test doubles continue to compile through default interface implementations.
- Inject `IBangumiAccountStore` into `BangumiGameMetadataProvider`. A valid account supplies the bearer token. A `401` or `403` marks the stored account as requiring reconnect, then the provider attempts the anonymous/legacy recovery path without exposing the token.
- The provider owns fallback mapping. A matching legacy result becomes linked partial metadata with the canonical subject URL.
- Persist an `IsPartial` flag with external metadata so the detail page still identifies partial data after save/restart and through Stage C cloud snapshots.
- `AddGameViewModel` and `GameDetailViewModel` map lookup states to precise Chinese status messages. Detail refresh supplies the local game name as the recovery query. The source action explains that the external browser has a separate login session.

## Data Flow

1. The view requests a lookup with subject ID and a title usable for recovery.
2. The provider loads the account. If usable, it requests `/v0/subjects/{id}` with `Authorization: Bearer ...`; otherwise it requests anonymously.
3. On authenticated `401/403`, the provider persists `RequiresReconnect=true` and continues with public recovery.
4. If details are unavailable, the provider calls `/search/subject/{query}?type=4&responseGroup=large&max_results=20` and accepts only the same subject ID.
5. A matching legacy result produces partial metadata. No match after both paths produces the not-found state.
6. Import/refresh persists the snapshot and its partial marker; selected-field behavior remains unchanged.

## Error Semantics

- **Complete:** details returned; all returned fields are eligible for import.
- **Partial:** details were unavailable but the same subject was recovered from large legacy search data.
- **Reconnect required:** stored credentials were already stale or received `401/403`; any recovered metadata remains usable, but the UI asks the user to reconnect.
- **Not found:** both detail lookup and same-ID legacy recovery failed. Only this state says the subject was deleted or no longer exists.
- Network, timeout, rate-limit, and server errors remain exceptions with their existing messages; they are not mislabeled as deletion.

## Cleanup Boundary

Delete tracked build outputs and obsolete visual verification artifacts under `.vs`, `_verify`, project `bin`/`obj`, and top-level prototype image folders only after proving the runtime assets already exist under `GameManager.App/Assets`. Add a root `.gitignore` to prevent regeneration. Keep `GameManager.App.Tests/Program.cs`, current plans/specs, product documents, and runtime assets.

## Verification

- Observe failing tests before production edits for bearer authentication, reconnect marking, large legacy fallback, self-healing detail refresh, partial persistence, and user-facing status text.
- Run the focused runner during each red-green cycle.
- Finish with the complete console test suite, a no-restore build, `git diff --check`, and a tracked-artifact scan.
