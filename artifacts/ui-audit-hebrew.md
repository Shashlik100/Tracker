# דוח audit רוחבי ל-UI

## מקומות שבהם נמצאו בעיות
- DailyDashboardControl: כפתורי תורים חכמים, כותרות summary, טבלת weak spots.
- ReviewFilterControl: כפתורי `התחל חזרה`, `המשך סשן`, שורות מסננים.
- SearchFilterControl: כפתורי `חפש`, `נקה`, ותיבות טקסט עם גובה קטן מדי.
- ReviewSessionControl: כפתורי reveal/rating/pause/skip/review later וסיכום סשן.
- StudyCardControl: כפתורי דירוג וניהול, וגובה כללי של הכרטיס.
- TagsManagerControl / ReviewPresetForm / TagEditForm / AddStudyItemForm / CsvImportForm: כפתורים, labels וטבלאות preview.
- MainForm: top menu, action bar, bulk action bar, toolbar buttons.

## הגורם המרכזי
רוב הבעיות נבעו משילוב של גדלים קשיחים קטנים מדי, חוסר padding אנכי, ו-controls שלא הותאמו באופן רוחבי לפונט עברי, RTL ו-DPI.

## מה תוקן
- נוספה שכבת `UiLayoutHelper` שמחילה metrics אחידים על כפתורים, טבלאות, labels, group boxes, tree views ו-toolstrips.
- הוגדלו גבהי controls, row heights ו-header heights.
- כפתורי פעולה מקבלים רוחב מינימלי מחושב לפי הטקסט בפועל.
- נוספו padding ויישור טקסט אחידים ל-RTL.
- כפתורי dashboard היו בגובה 28 ורוחב 150, ולכן כיתובים כמו 'התחל חזרה יומית' נחתכו.
- כפתורי מסננים וחיפוש השתמשו בשורות 34 פיקסל עם RTL עברי, מה שגרם לחיתוך אנכי.
- DataGridView-ים לא הגדירו header/row height מספקים לעברית ול-DPI.
- כרטיסי review ו-dialogs השתמשו בכפתורים קשיחים קטנים מדי וללא padding אחיד.