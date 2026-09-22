# Till Winter — Store Package

Everything both consoles ask for, in one place. Made by EFS Games. Bundle id
`com.efsgames.tillwinter`, version 1.0.0. Portrait, offline, no accounts, no ads, no analytics,
no network requests, saves only on device. English and Turkish.

---

## 1. Listing texts

### App name

- EN: **Till Winter**
- TR: **Till Winter** (unchanged — it reads fine as a loanword and keeps the wordplay)

### Subtitle / short description

- Apple subtitle (≤30 chars): `Water, grow, harvest, offline` (29 chars)
- Apple subtitle TR (≤30 chars): `Su, büyüt, hasat, çevrimdışı` (28 chars)
- Play short description (≤80 chars): `A one-thumb farming game across four seasons, offline, five hours to an ending.` (79 chars)
- Play short description TR (≤80 chars): `Dört mevsim boyunca tek parmakla çiftçilik, çevrimdışı, sona beş saat.` (70 chars)

### Full description (EN, ≤4000 chars — Play and Apple both use this)

```
A ring follows your thumb across the field. Every plot under it waters, grows and pops into
coins — one finger, always busy.

A year is about four minutes. Spring rains water for you, summer dries the plots out, autumn
stretches your harvest combo, and the last ten seconds before frost pay more and beat like a
heart. Then winter opens the Almanac, a skill tree of 38 nodes across five branches: a wider
ring, irrigation and sun that work while you're away, apprentices who harvest on their own, a
tractor, scarecrows, a dog, hens, a beehive, a barn that stores part of each harvest for the
market.

When a farm stalls, you pass it on. The coins and the field are gone; Heritage Seeds remain and
buy permanent nodes for every generation that follows. Each heir brings a trait, challenge
generations earn extra seeds, and achievements become heirlooms that never leave the family.

Along the way: pests, weather, yearly goals and grades, a travelling trader, crop rotation.

Finish the Heritage tree and you play one Golden Year on a full field of golden wheat. After
that: a family album of every generation, New Game+ for a harder run, and a daily farm — the
same field and seed for everyone that day, one year, one score.

No ads, no purchases, no internet connection needed — the whole game runs and saves on your
device. A farm code moves your save to another phone.

Five to seven hours to the ending. Then it ends, on purpose.
```

### Full description (TR, ≤4000 chars)

```
Bir halka parmağını takip eder ve altındaki her tarla sulanır, büyür ve altına dönüşür — tek
parmak, hep meşgul.

Bir yıl yaklaşık dört dakika sürer. İlkbahar yağmurları sizin yerinize sular, yaz tarlaları
kurutur, sonbahar hasat komboyu uzatır ve dondan önceki son on saniye daha çok öder, bir kalp
gibi atar. Ardından kış, beş dalda 38 düğümlük bir beceri ağacı olan Almanak'ı açar: daha geniş
bir halka, siz yokken çalışan sulama ve güneş, kendi başına hasat yapan çıraklar, traktör,
korkuluklar, köpek, tavuklar, arı kovanı, her hasadın bir kısmını pazar için saklayan ambar.

Bir çiftlik durduğunda onu bir sonraki nesle devredersiniz. Altınlar ve tarla gider; Miras
Tohumları kalır ve sonraki her nesil için kalıcı düğümler satın alır. Her varis bir özellik
getirir, zorluk nesilleri ekstra tohum kazandırır ve başarımlar aileden hiç ayrılmayan miraslara
dönüşür.

Yol boyunca: zararlılar, hava durumu, yıllık hedefler ve notlar, gezici bir tüccar, ekin
rotasyonu.

Miras ağacını tamamlayınca tam bir altın buğday tarlasında bir Altın Yıl oynarsınız. Ardından:
her neslin olduğu bir aile albümü, daha zor bir tur için Yeni Oyun+ ve günlük çiftlik — o gün
herkes için aynı tarla ve tohum, tek yıl, tek skor.

Reklam yok, satın alma yok, internet bağlantısı gerekmiyor — oyun tamamen cihazınızda çalışır ve
kaydeder. Bir çiftlik kodu kaydinizi başka bir telefona taşır.

Sona beş ila yedi saat. Sonra, bilerek biter.
```

### Keywords / tags

- Apple keywords (≤100 chars, comma-separated, EN): `farming,incremental,idle,offline,skill tree,relaxing,casual,simulation,no ads,farm` (80 chars)
- Apple keywords TR: `çiftlik,idle,tembel oyun,çevrimdışı,beceri ağacı,rahatlatıcı,simülasyon,reklamsız`
- Play tags (suggested, pick from Play Console's list): Farming, Simulation, Casual, Idle

### What's new (1.0.0)

- EN: `First release.`
- TR: `İlk sürüm.`

### Promotional text (Apple, ≤170 chars)

`A farm that ends. Water, grow and harvest with one ring across four seasons, build permanent Heritage, and finish in one Golden Year.` (134 chars)

---

## 2. Categories and ratings

- Apple: primary **Games → Simulation**, secondary **Games → Casual**.
- Play: category **Simulation**.

### IARC questionnaire answers

All "no": no violence, no sex, no profanity/language, no controlled substances, no gambling, no
user interaction (no chat, no user-generated content, no location sharing), no personal
information shared, no location access, no purchases.

Expected outcome from those answers: **PEGI 3**, **ESRB E**, **USK 0**, **Apple 4+**.

### Apple age rating form

Every category: **None**.

### Play "Target audience and content"

Set target audience to **13+** to skip the Designed for Families programme's extra requirements
(ads/analytics SDK restrictions, additional review, a families-specific privacy policy). Trade-off:
the listing won't appear under Play's Families tab, but nothing about the game requires that
programme.

---

## 3. Data safety / privacy forms

### Play Data safety

- Data collected: **none**.
- Data shared: **none**.
- No "encrypted in transit" claim needed — there is no transit.
- Data deletion: uninstalling the app removes the save, or use **Settings → Reset save** in-game.

### Apple App Privacy

- Answer: **Data Not Collected**.

### Privacy policy URL

`https://github.com/emirsakal/TillWinter/blob/main/docs/PRIVACY.md`

(A hosted page can replace this link later; the raw GitHub file works for submission today.)

### Apple `PrivacyInfo.xcprivacy`

- Tracking: **NO**.
- Collected data types: **none**.
- Required reason API categories accessed:
  - `NSPrivacyAccessedAPICategoryUserDefaults` — reason `CA92.1` (user preferences stored and
    read only on this device).
  - `NSPrivacyAccessedAPICategoryFileTimestamp` — reason `C617.1` (save file timestamp read for
    the game's own save management).

Unity's own manifest already declares its engine-level API usage; the app's `PrivacyInfo.xcprivacy`
entries above are written by the iOS build post-process, not hand-edited.

---

## 4. Trader / contact (EU DSA)

Both Apple and Google require a trader declaration for apps distributed in the EU, even free ones.
This goes into each console's account settings, never into the repo: legal name, address, and a
contact email or phone number.

Support URL for both listings: `https://github.com/emirsakal/TillWinter/issues`

Contact email for the trader forms: `<the developer fills this in>`

---

## 5. Assets checklist

### Google Play

- Icon: 512×512 PNG.
- Feature graphic: 1024×500 (`Assets/Art/Icon/SplashLogo.png` is a candidate source to crop/pad
  from).
- Phone screenshots: minimum 2, aspect ratio between 16:9 and 9:16, shortest side ≥320 px, longest
  side ≤3840 px.
- 7" and 10" tablet screenshots.
- Promo video: optional.

### Apple App Store

- iPhone 6.9" screenshots: 1320×2868 (current required set for newer devices).
- iPhone 6.5" screenshots: 1284×2778 (optional, older device coverage).
- App preview video: optional.
- Icon: 1024×1024, no alpha channel.

### How to get them

`ui-tour.bat <folder> "<preset>" clean` opens every screen and sheet in play mode and writes
`NN-name.png` plus `report.txt` to `<folder>`; the `clean` argument hides the developer button so
the shots are store-ready. Presets available today: `1080x2340 (Portrait)` (default),
`1080x1920 (Portrait)`, `1080x2400 (Portrait)`, `1536x2048 (Tablet)` and `1320x2868 (iPhone 6.9)`
for the Apple set. The shots for this release live in `docs/store/`.

### Recommended shot list (8)

1. Title screen
2. Spring field with the ring
3. Autumn dusk
4. The seed bag
5. The Almanac tree
6. A node detail sheet
7. The Heritage tree
8. The family album

---

## 6. Pre-submission checklist

- [ ] Signed `.aab` produced by `build-android.bat` with `TW_KEYSTORE_PATH`, `TW_KEYSTORE_PASS`,
      `TW_KEY_ALIAS`, `TW_KEY_PASS` set for that shell only.
- [ ] Xcode archive built and validated on a Mac.
- [ ] Both listings' texts (EN + TR) pasted into their consoles.
- [ ] Screenshots uploaded for every required size (phone, tablet, and each Apple device class).
- [ ] Privacy policy URL set on both consoles.
- [ ] Play Data safety form submitted.
- [ ] Content rating questionnaire completed (both consoles).
- [ ] Trader declaration entered (both consoles).
- [ ] Tested on two real devices per `docs/DEVICE-CHECKLIST.md`.
- [ ] Version code (Android) / build number (iOS) noted for this submission.
