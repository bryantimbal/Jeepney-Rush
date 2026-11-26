# New version on Google Play (app updates)

At the main menu (once per app start) the game checks for a newer version:

1. **Google Play In-App Updates** (recommended – Play itself says whether an update exists):
   * optional update → "MAY BAGONG VERSION!" ▸ **UPDATE** downloads it in the background while
     you play ▸ **I-RESTART** installs it; **MAMAYA NA** skips that version.
   * high priority (Play Console *update priority* 4–5) or required (server minimum, below) →
     Play's full-screen update.
2. **Server backup** (editor, plugin not installed, sideloaded APKs): Supabase table
   `jr_app_release` (newest `latest_code`, oldest allowed `min_code`). An older app gets the same
   dialog with **UPDATE** opening the Play Store page. Below `min_code` the dialog can't be closed.

## Setup (once)
1. Download **Google Play In-App Update** for Unity (`com.google.play.appupdate`, from
   <https://developers.google.com/unity/packages#play_in-app_update> – the `.tgz` or `.unitypackage`)
   and add it: Window ▸ Package Manager ▸ **+ ▸ Add package from tarball…** (it also brings
   `com.google.play.common` and the External Dependency Manager; run *Assets ▸ External
   Dependency Manager ▸ Android Resolver ▸ Resolve*).
2. Project Settings ▸ Player ▸ Android ▸ **Scripting Define Symbols**: add `JR_PLAY_UPDATES`
   (next to `JR_ADMOB;JR_UNITY_IAP` if you use them).
3. Apply the Supabase migration `20261126120000_jr_app_version.sql`.

Without step 1–2 the game still builds and uses the server backup only.

## Every release
1. Build, upload, roll out on Google Play as usual (`docs/RELEASE.md`).
2. **Jeepney Rush ▸ Release ▸ Mark Current Version As Uploaded** – it also copies a line of SQL.
3. When the update is **live** on Google Play, paste that SQL in **Supabase ▸ SQL Editor**:
   ```sql
   update public.jr_app_release set latest_code = 12, latest_name = '1.2.0', message = 'Bagong Ghost Race!' where id = 1;
   ```
   (`message` is shown in the dialog.) Raise `min_code` only when old versions must stop working.

## Testing
In-App Updates only work for an app **installed from Google Play** (use *Internal testing* or
*Internal app sharing*): install version N from Play, upload N+1, open the game. The server
backup can be tested in the Editor: set `latest_code` above Player Settings ▸ Bundle Version Code.
