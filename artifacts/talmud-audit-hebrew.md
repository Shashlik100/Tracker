# Audit תלמוד - דוח שלב

## מצב קודם בקוד
- ענף התלמוד כבר היה קיים במערכת, אבל נשען בעיקר על mapping פנימי בתוך LibrarySeedFactory.
- המבנה הקודם היה לא קנוני ביחס למודל החדש: תלמוד -> תלמוד בבלי / תלמוד ירושלמי, ולא תלמוד -> בבלי / ירושלמי.
- בבלי וירושלמי השתמשו שניהם ב-metadata פנימי; ירושלמי לא היה מחובר בפועל למבנה source-backed של פרק -> הלכה.
- מבנה lazy loading היה חלקי: צמתי התלמוד נטענו דרך seed פנימי, בלי audit מלא של aliases, שמות עבריים קנוניים, או תאימות מלאה ל-Sefaria.
- הייתה סכנה של ענפים גנריים/חלקיים במסד אם כרטיסי דוגמה נזרעו לפני שטעינה עצלה בנתה את הצמתים הקנוניים.

## מה נבדק ב-audit
- נבדקה זריעת ברירת המחדל של התלמוד בקוד וב-SQLite.
- נבדקו aliases ושמות לא אחידים כמו Babylonian Talmud, Jerusalem Talmud, תלמוד בבלי, תלמוד ירושלמי, Bavli, Yerushalmi.
- נבדקה שלמות ההיררכיה וה-load on demand של:
  - תלמוד -> בבלי -> סדר -> מסכת -> פרק -> דף
  - תלמוד -> ירושלמי -> סדר -> מסכת -> פרק -> הלכה
- נבדק שימוש ב-Sefaria כמקור פתוח אמין:
  - pi/index עבור אוספים, סדרים ומסכתות
  - pi/index/{tractate} לצורת חלוקה
  - pi/shape/{tractate} עבור ירושלמי ומבנה פרקים/הלכות
- נבדקה שלמות הקשרים למערכת הקיימת: כרטיסים, תגיות, חיפוש, review, queues, presets ו-dashboard.

## מה היה חסר או לא עקבי
- השורש התלמודי לא היה מנורמל לשמות בבלי ו-ירושלמי.
- בבלי לא היה source-backed ברמת סדר/מסכת, אלא פנימי בלבד.
- ירושלמי לא היה מנוהל במודל הקנוני הנכון של פרק -> הלכה.
- היו מפתחות sourceKey עמומים שגרמו לטעינה לא נכונה בין סדר/מסכת/פרק בירושלמי עד שתוקנו.
- כרטיסי דוגמה יכלו ליצור צמתים גנריים מוקדמים מדי תחת התלמוד.

## מה תוקן והושלם
- שורש התלמוד נרמל ל-תלמוד -> בבלי / ירושלמי.
- נוסף שירות חדש SefariaTalmudService שמביא מ-Sefaria:
  - אוספים
  - סדרים
  - מסכתות
  - shapes של ירושלמי
- בבלי עודכן למודל hybrid:
  - סדרים ומסכתות מ-Sefaria
  - פרקים ודפים ממיפוי פנימי מתוקנן
- ירושלמי עודכן למודל source-backed:
  - סדרים ומסכתות מ-Sefaria
  - פרקים והלכות מ-pi/shape
- ה-lazy loading חובר מחדש כך שרק מה שנפתח נטען בפועל.
- SourceKey נורמל למפתחות מפורשים (seder, 	ractate, chapter, page, halakhah) כדי למנוע ambiguity.
- audit/normalization עודכנו כדי:
  - לנרמל aliases
  - לשמר שיוכי כרטיסים/פריסטים
  - לעדכן שמות קנוניים לצמתי SefariaTalmud
- כרטיסי דוגמה שונו כך שלא יזרעו עומק תלמודי גנרי לפני טעינה עצלה.

## מה עדיין לא הושלם
- בבלי עדיין אינו source-backed מלא ברמת גבולות פרק -> דף; החלק הזה נשאר ממיפוי פנימי מתוקנן.
- ירושלמי נעצר כרגע ב-הלכה ולא יורד ל-segment, כדי לשמור על עץ שימושי וזריז.
- verification מלא על מסד המשתמש האמיתי לא בוצע, כי הגישה לקובץ נחסמה בהרשאות.

## verification בפועל
- verification אמיתי רץ מתוך האפליקציה על מסד מבודד.
- צילום מסך נוצר בפועל.
- report JSON נוצר בפועל.
- fetch מ-Sefaria עבד באמת, כולל pi/index ו-pi/shape למסכתות ירושלמי.

### תוצאות verification
- RootCount: 3
- LoadedSubjectCount: 67
- TalmudCollectionCount: 2
- TalmudSederCount: 11
- TalmudTractateCount: 76
- TalmudChapterCount: 618
- TalmudLeafCount: 5160
- BavliSederCount: 6
- BavliTractateCount: 37
- BavliChapterCount: 321
- BavliPageCount: 2949
- YerushalmiSederCount: 5
- YerushalmiTractateCount: 39
- YerushalmiChapterCount: 297
- YerushalmiHalakhahCount: 2211
- DuplicateSiblingGroupCount: 0
- SelectedBavliPath: תלמוד / בבלי / סדר זרעים / ברכות / פרק א / דף ב
- SelectedYerushalmiPath: תלמוד / ירושלמי / סדר זרעים / ברכות / פרק א / הלכה א
- Success: True

## מגבלת מסד המשתמש האמיתי
- הקובץ האמיתי זוהה בנתיב:
  - C:\Users\daniel\AppData\Local\TrackerApp\tracker.db
- ניסיון להעתיק או לעבוד עליו מתוך הסביבה נכשל עם:
  - Access to the path 'C:\Users\daniel\AppData\Local\TrackerApp\tracker.db' is denied.
- לכן ה-verification הסופי רץ על מסד מבודד בנתיב:
  - C:\CodexProjects\Tracker\TestData\talmud_verify_run2

## Roadmap קצר להמשך ענף התלמוד
1. להחליף את מיפוי הפרקים-הדפים של בבלי במקור פתוח מדויק יותר אם יימצא מקור אמין.
2. להוסיף רמת עמוד או שורה לבבלי אם יוחלט שה-workflow הלימודי דורש זאת.
3. להוסיף רמת segment לירושלמי כאופציה מתקדמת, לא כברירת מחדל.
4. להמשיך אחרי זה לרמב"ם, טור ושולחן ערוך על אותו מודל source-backed + lazy loading + audit.
