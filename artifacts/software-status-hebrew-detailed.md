# דוח מצב מפורט מאוד - TrackerApp

## 1. מטרת המוצר
האפליקציה נבנתה כאפליקציית Desktop מקומית ללימוד בשיטת חזרות מרווחות, עם אופי תורני, אחסון מקומי ב-SQLite, וממשק WinForms עברי RTL בסגנון תוכנות ישנות/צפופות.  
המטרה בפועל כרגע היא להיות בסיס עובד למערכת לימוד עם:
- ספרייה תורנית היררכית
- כרטיסי שאלה/תשובה
- תזמון חזרות
- סטטיסטיקות
- ייצוא/הדפסה/גיבוי

## 2. מצב כללי של הפרויקט
הפרויקט נמצא בשלב מתקדם יחסית של MVP עובד, אבל עדיין לא מוצר גמור ב-100%.

מה שכבר יש:
- אפליקציה נפתחת ועולה
- ממשק עברי RTL
- מסד SQLite מקומי
- עץ ספרייה
- כרטיסי לימוד
- לוגיקת חזרות
- סטטיסטיקות
- גיבוי וייצוא
- אינטגרציה אמיתית עם Sefaria לתנ״ך

מה עדיין לא מושלם:
- יש חלקים שהם placeholder בלבד
- יש חלקים שה-UI שלהם קיים אבל לא הפונקציונליות
- יש אזורים שעדיין לא polished מספיק
- חלק מהמערכת עובד רק חלקית או לא עד הסוף

## 3. טכנולוגיה וארכיטקטורה

### UI
- הטכנולוגיה היא WinForms.
- הממשק בנוי כחלון ראשי אחד.
- יש top chrome עם tabs וסרגל formatting.
- יש TreeView ימני.
- יש אזור תוכן שמאלי עם כרטיסים או סטטיסטיקות.

### Persistence
- כל הנתונים נשמרים ב-SQLite מקומי.
- הטבלאות המרכזיות:
  - `Subjects`
  - `StudyItems`
  - `ReviewHistory`
  - `AppMetadata`

### Network
- יש אינטגרציה מול Sefaria.
- הקריאות מבוצעות מול API ציבורי.
- יש cache מקומי כדי לא למשוך שוב ושוב את אותם נתונים.

### Data model
- `Subjects` מייצגת את עץ הספרייה.
- `StudyItems` מייצגת את הכרטיסים.
- `ReviewHistory` שומרת חזרות שבוצעו בפועל.
- `AppMetadata` משמש למידע מערכת כמו גרסאות seed.

## 4. מה נבדק בפועל, לא רק "לפי הקוד"

בוצעה הרצה אמיתית של התוכנה.

בבדיקה הזו:
- האפליקציה נפתחה בפועל
- עץ הספרייה נפתח בפועל
- `תנ״ך` נפתח
- `תורה` נפתחה
- `נביאים` נפתחו
- `כתובים` נפתחו
- נפתחו ספרים בפועל
- נפתח פרק בפועל
- נפתחו פסוקים בפועל
- הופקו screenshots מתוך החלון עצמו
- נשמר דוח JSON של verification

קבצי הוכחה:
- `C:\CodexProjects\Tracker\artifacts\tanakh-overview-fixed.png`
- `C:\CodexProjects\Tracker\artifacts\tanakh-detail-fixed.png`
- `C:\CodexProjects\Tracker\artifacts\tanakh-report-fixed.json`

## 5. מודול הספרייה - מצב מפורט

### 5.1 תשתית עץ הספרייה
המערכת מחזיקה עץ היררכי ב-`Subjects`.

לכל node יש:
- `Id`
- `ParentId`
- `Name`
- `NodeType`
- `SourceSystem`
- `SourceKey`
- `SortOrder`

זה אומר שהעץ כבר לא "רק טקסט", אלא בנוי בצורה שאפשר:
- להרחיב אותו לעומק
- לזהות סוג node
- לדעת מאיזה מקור הוא מגיע
- לבצע lazy loading

### 5.2 lazy loading
העץ לא טוען את הכול מראש.

מה זה אומר בפועל:
- root nodes נטענים קודם
- child nodes נטענים רק כאשר פותחים node
- זה קריטי במיוחד לתנ״ך, כי פרקים ופסוקים יכולים להיות אלפי nodes

זה עובד דרך:
- `TreeView.BeforeExpand`
- `EnsureSubjectChildrenLoaded`
- זיהוי `HasChildren`
- dummy node זמני

### 5.3 תנ״ך
זה החלק שהכי נבדק בפועל כרגע.

קיים:
- root `תנ״ך`
- child sections:
  - `תורה`
  - `נביאים`
  - `כתובים`

התנהגות:
- פתיחת `תנ״ך` מציגה את שלוש הקטגוריות
- פתיחת `תורה` טוענת ספרים
- פתיחת ספר טוענת פרקים
- פתיחת פרק טוענת פסוקים

זה עובד מול Sefaria ולא דרך hardcode מלא של כל העץ.

### 5.4 אינטגרציית Sefaria לתנ״ך
האינטגרציה כרגע משתמשת בפועל ב:
- `api/index`
- `api/shape/{book}`
- `api/v3/texts/{book chapter}`

מה נשלף משם:
- רשימת ספרים לפי קטגוריה
- מספר פרקים
- מספר פסוקים בכל פרק

### 5.5 caching
יש cache מקומי.

זה אומר:
- אם כבר הורדו נתוני TOC, לא מביאים שוב כל פעם
- אם כבר הורדו shape או chapter data, משתמשים ב-cache
- האפליקציה מגיבה מהר יותר

### 5.6 normalization
נוסף גם מנגנון שמנקה שמות ספרים שכבר נשמרו קודם בג׳יבריש.

כלומר:
- אם tree nodes ישנים נשמרו לא נכון
- בעת startup יש תיקון אוטומטי לפי `SourceKey`

### 5.7 מה עובד מלא בספרייה
- RTL
- TreeView ימני
- תנ״ך
- תורה/נביאים/כתובים
- ספרים בעברית
- פרקים בעברית
- פסוקים בעברית
- lazy loading
- Sefaria fetch
- cache
- normalization

### 5.8 מה עובד חלקי בספרייה
- תלמוד קיים בעץ ובזריעה, אבל לא בוצע לו verification חזותי מקיף כמו לתנ״ך בסבב האחרון
- יש support backend לעומק hierarchy, אבל לא כל מסלול נבחן ידנית באותה רמת עומק

### 5.9 מה עדיין לא מושלם בספרייה
- יש קוד ישן/כפול מסביב לשירותי Sefaria
- יש עדיין חלק מהתרגומים מפוזרים בכמה קבצים
- עץ הספרייה הכללי לא מכסה עדיין "כל ספריה יהודית אפשרית", אלא כרגע בעיקר את התנ״ך ועוד שכבות seed נוספות

## 6. כרטיסי לימוד - מצב מפורט

### 6.1 תצוגת כרטיס
כל כרטיס מציג:
- כותרת
- נתיב נושא
- תאריך יעד
- שאלה
- תשובה
- מטא-דאטה על החזרה

### 6.2 כפתורי דירוג
יש 5 כפתורי דירוג:
- גרוע
- חלש
- בסדר
- טוב
- מצוין

הם מחוברים בפועל ל-lifecycle של הכרטיס ולמסד.

### 6.3 יצירה / עריכה / מחיקה
יש:
- יצירת כרטיס חדש
- עריכת כרטיס
- מחיקת כרטיס

זה מחובר למסד בפועל.

### 6.4 מה עובד מלא
- תצוגת כרטיסים
- יצירת כרטיס
- עריכת כרטיס
- מחיקת כרטיס
- דירוג
- שמירת שינויים

### 6.5 מה חסר / חלקי
- אין כרגע rich text editing אמיתי בכרטיסים
- שורת formatting העליונה לא באמת משנה את תוכן הכרטיס
- אין כעת מנגנון tagging אמיתי לכרטיסים

## 7. מנגנון spaced repetition - מצב מפורט

### 7.1 מה ממומש
יש מימוש של אלגוריתם SM-2 style.

המערכת מחשבת:
- repetition count
- ease factor
- interval days
- lapses
- next due date

### 7.2 מה נשמר במסד
ב-`StudyItems` נשמר:
- `RepetitionCount`
- `Lapses`
- `EaseFactor`
- `IntervalDays`
- `LastRating`
- `LastReviewedAt`

ב-`ReviewHistory` נשמר:
- זמן חזרה
- rating
- score
- success/failure
- ease before/after
- interval before/after

### 7.3 מה עובד מלא
- לחיצה על דירוג מעדכנת due date
- הנתונים נשמרים
- history נרשמת
- status view יכול להתבסס על review history

### 7.4 מה חלקי
- זה "SM-2 style" ולא מנוע זיכרון סופר־מתקדם כמו מוצר מסחרי בוגר
- אין כרגע tuning UX מתקדם לכל edge case

## 8. סטטיסטיקות ודשבורד - מצב מפורט

### 8.1 מה קיים
יש מסך מצב.

הוא מציג:
- סה"כ פריטים
- לביצוע היום
- חזרות היום
- אחוז שימור
- נלמד היטב
- bar charts
- line chart
- heatmap

### 8.2 איך זה עובד
הנתונים נמשכים מ:
- `StudyItems`
- `ReviewHistory`

יש חישובים ל:
- retention rate
- completed today
- due today
- mastered items
- grouped counts by category

### 8.3 מה עובד מלא
- המסך נפתח
- הנתונים נטענים
- הגרפים מצוירים
- heatmap מצויר

### 8.4 מה חלקי
- הגרפים custom painted ופשוטים יחסית
- אין drill-down
- אין export לסטטיסטיקות
- אין פילטור מתקדם

### 8.5 מה סטטי
- שום chart כאן לא "מזויף" לגמרי; כולם מבוססים על נתונים
- אבל העיצוב הוא בסיסי ולא polished כמו מוצר מסחרי מלא

## 9. גיבוי / ייצוא / הדפסה

### 9.1 גיבוי
הגיבוי עובד דרך:
- `VACUUM INTO`

זה אומר:
- אפשר לייצר קובץ DB backup
- מדובר בגיבוי אמיתי של SQLite

### 9.2 ייצוא CSV
קיים export ל-CSV.

נשמרים:
- Id
- SubjectPath
- Topic
- Question
- Answer
- DueDate
- EaseFactor
- IntervalDays
- RepetitionCount
- LastRating

### 9.3 import CSV
יש backend של import.

זה עובד על:
- `SubjectPath`
- `Topic`
- `Question`
- `Answer`

אבל:
- אין כרגע flow UI גמור ונוח ל-import מתוך המסך הראשי

### 9.4 הדפסה לשבת
יש dialog לבחירת טווח תאריכים.

יש ייצוא ל-HTML printable.

### 9.5 מה עובד מלא
- backup DB
- export CSV
- dialog לטווח הדפסה
- HTML export

### 9.6 מה חלקי
- import CSV קיים אבל לא מחובר טוב ל-UI
- PDF לא קיים

### 9.7 מה סטטי
- ההדפסה לא "רק כפתור", היא באמת מוציאה HTML
- אבל היא לא ברמת print pipeline מתקדמת

## 10. הסרגל העליון והלשוניות

### 10.1 מה עובד
- לשונית שאלות
- לשונית מצב
- גיבוי
- עזרה
- אודות

### 10.2 מה רק placeholder
- תגיות
- חיפוש
- הגדרות

המשמעות:
- יש כפתור
- יש טקסט
- יש event handler
- אבל בפועל הוא רק מציג status text ולא מערכת אמיתית

### 10.3 שורת formatting
קיימת שורת עיצוב עשירה למראה.

אבל בפועל:
- אין לה חיבור ל-editor
- היא כרגע ויזואלית בלבד

כלומר:
- זה לא שבור
- זה פשוט לא ממומש פונקציונלית

## 11. עזרה / אודות

### עובד
- שני החלונות נפתחים
- מוצגים MessageBox בעברית

### לא קיים
- אין מערכת עזרה אמיתית
- אין walkthrough
- אין help pages

## 12. עברית / RTL / עיצוב

### מה עובד
- ה-UI כולו מוגדר RTL
- TreeView ימני
- כרטיסים RTL
- חלונות dialog RTL
- top menu בעברית

### מה עבר תיקון
- mirror של החלון
- toolbars שנבלעו/נחתכו
- layout docking של הסרגלים
- טקסטים שנחתכו
- ג׳יבריש בספרי תנ״ך

### מה עדיין לא מושלם
- חלק מקבצי הקוד, כשפותחים אותם ב-shell לא נכון, נראים בג׳יבריש. זו בעיית encoding של הטרמינל/קובץ, לא בהכרח של ה־UI בזמן ריצה.
- יש עדיין strings באנגלית בחלק ממודולי export/print

## 13. SQLite / מסד / schema

### מה יש
טבלאות:
- `Subjects`
- `StudyItems`
- `ReviewHistory`
- `AppMetadata`

### שדרוגים שנוספו
`Subjects`:
- `NodeType`
- `SourceSystem`
- `SourceKey`

`StudyItems`:
- `ModifiedAt`
- `RepetitionCount`
- `Lapses`
- `EaseFactor`
- `IntervalDays`
- `LastRating`

`ReviewHistory`:
- `Rating`
- `WasSuccessful`
- `EaseFactorBefore`
- `EaseFactorAfter`
- `IntervalBefore`
- `IntervalAfter`

### מה עובד
- schema creation
- schema upgrade
- seeding
- normalization
- data reads/writes

## 14. שגיאות ובעיות שכבר נפתרו

נפתרו בפועל במהלך העבודה:
- startup crash שקט
- בעיות reset של DB
- חסימות הרשאות לנתיב DB
- חפיפה של toolbars
- clipping של טקסט עברי
- RTL inversion של ה-window chrome
- metadata parser crash
- failure ב-lazy loading
- ג׳יבריש בשמות ספרי תנ״ך
- parsing של `api/shape/...` בפורמט `chapters`
- מיפוי ספרים מ-`api/index`

## 15. מה עובד מלא מול מה עובד חלקי מול מה לא עושה כלום

### עובד מלא
- פתיחת אפליקציה
- טיפול שגיאות גלובלי
- SQLite מקומי
- schema upgrade
- הוספה/עריכה/מחיקה של כרטיסים
- דירוגים 5 מצבים
- SM-2 style scheduling
- history של חזרות
- status dashboard
- backup
- export CSV
- export HTML להדפסה
- תנ״ך מ-Sefaria עד רמת פסוק
- lazy loading
- screenshots verification

### עובד חלקי
- תלמוד בעץ
- import CSV
- dashboard polish
- אינטגרציה רחבה יותר עם Sefaria מעבר לתנ״ך
- שלמות ה-clone הוויזואלי
- הדפסה ברמת מוצר מלאה

### סטטי / placeholder
- תגיות
- חיפוש
- הגדרות
- formatting toolbar
- חלק מה-help/about

## 16. מה יש ב-backend אבל לא בחזית
- `ImportFromCsv(...)`
- `AddSubjectFolder(...)`
- חלק מאפשרויות ניהול ספרייה

## 17. סיכונים טכניים להמשך
- כפילות בין שירות Sefaria חדש לישן
- ריבוי strings/translation logic בכמה קבצים
- encoding consistency
- חלקי UI שעדיין רק "נראים" מוכנים
- עץ תורני גדול מאוד יכול לדרוש עוד אופטימיזציה אם מרחיבים גם למסלולים נוספים

## 18. קבצים מרכזיים בפרויקט
- `MainForm.cs` - shell ראשי, tree, tabs, self-test
- `StudyCardControl.cs` - כרטיסי לימוד
- `StatusViewControl.cs` - מסך מצב
- `AddStudyItemForm.cs` - יצירה/עריכה
- `PrintRangeForm.cs` - בחירת טווח הדפסה
- `AppDatabase.cs` - ליבת DB
- `AppDatabase.Library.cs` - schema/seed/library maintenance
- `AppDatabase.Tools.cs` - tree loading/tools/import/export/stats
- `SpacedRepetitionCalculator.cs` - מנגנון חזרות
- `SefariaTanakhService.cs` - אינטגרציית תנ״ך מול Sefaria

## 19. מסקנה מוצרית
המצב כרגע הוא לא "רק דמו" ולא "רק mockup".  
זה מוצר עובד ברמת בסיס־ממשי עם הרבה backend ו-UI אמיתיים, כולל אינטגרציה חיה, אבל עדיין לא מוצר מסחרי גמור עד הסוף.

במונחים פשוטים:
- זה לא רק שלד
- זה לא רק כפתורים יפים
- יש כבר הרבה מאוד יכולת אמיתית
- אבל עדיין לא הכול מחובר או מלוטש

## 20. סדר עדיפויות מומלץ להמשך
1. לחבר Import CSV ל-flow UI מסודר.
2. לממש חיפוש אמיתי.
3. לממש תגיות אמיתיות.
4. לממש הגדרות אמיתיות.
5. לנקות שירותי Sefaria וקוד כפול.
6. לאחד ולנקות translation/RTL helpers.
7. להרחיב verification גם למסלולי תלמוד.
8. להוסיף PDF אמיתי.
9. לשפר polish ויזואלי.
