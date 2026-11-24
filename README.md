# CC Petomar – program pre Windows

Okno s CC (`http://192.168.250.181:3000/cc`) cez Microsoft Edge WebView2.

- pri prichádzajúcom hovore (zvoní / ste ďalší v poradí / volá kolega) okno vyskočí pred všetky okná
- zvuky (správy kolegov) bez kliknutia
- krížik = program beží ďalej pri hodinách; úplne ukončiť: pravý klik na ikonu CC → Ukončiť program
- štart so systémom Windows, ikona na ploche a v ponuke Štart
- automatická aktualizácia: najvyššie `cc-petomar-win-vN.exe` v cc/server (server v66: `/api/win-verzia`, `/win-exe`)
- údaje: `%LOCALAPPDATA%\CCPetomar` (nastavenia.json, zaznam.txt, WebView2), program: `%LOCALAPPDATA%\Programs\CC Petomar`

Nová verzia: zvýšiť `VERZIA` v Program.cs → push → GitHub Actions „Postav EXE“ → artefakt `cc-petomar-win-vN.exe` → nahrať do cc/server.
Prvá inštalácia na PC: prehliadač `http://192.168.250.181:3000/win-exe` → spustiť (SmartScreen: Ďalšie informácie → Spustiť aj tak).
