using System;
using UnityEngine;
using UnityEditor;

public enum eMonthName
{
    January = 1,
    February,
    March,
    April,
    May,
    June,
    July,
    August,
    September,
    October,
    November,
    December,
}

public class DatePickerWindow : EditorWindow
{
    public static DatePickerWindow m_DatePicker;
    public static Action<DateTime> OnDatePicked;

    private DateTime m_CurrentDateTime;

    private int m_SelectedYear;
    private int m_SelectedMonth;
    private int m_SelectedDay;
    private int m_SelectedHour;
    private int m_SelectedMinute;
    private int m_SelectedSeconds;

    private bool m_UseUTC = true;

    private Texture2D selectedDayTexture;
    private const string TIME_MODE_PREF_KEY = "DatePickerWindow_TimeModeUTC";

    #region Window Setup

    public static void ShowWindow()
    {
        m_DatePicker = GetWindow<DatePickerWindow>(false, "Pick a Date", true);
        m_DatePicker.maxSize = new Vector2(260f, 420f);
        m_DatePicker.minSize = m_DatePicker.maxSize;
        m_DatePicker.Show();
        m_DatePicker.Init();
    }

    public static void CloseWindow()
    {
        if (m_DatePicker != null)
            m_DatePicker.Close();
    }

    private void OnEnable()
    {
        CreateTextures();

        m_UseUTC = EditorPrefs.GetBool(TIME_MODE_PREF_KEY, true);

        Init();
    }

    private void OnFocus()
    {
        Init();
    }

    #endregion

    #region Initialization

    public void Init()
    {
        SetCurrentTime();
    }

    private void SetCurrentTime()
    {
        m_CurrentDateTime = m_UseUTC ? DateTime.UtcNow : DateTime.Now;

        m_SelectedDay = m_CurrentDateTime.Day;
        m_SelectedMonth = m_CurrentDateTime.Month;
        m_SelectedYear = m_CurrentDateTime.Year;
        m_SelectedHour = m_CurrentDateTime.Hour;
        m_SelectedMinute = m_CurrentDateTime.Minute;
        m_SelectedSeconds = m_CurrentDateTime.Second;
    }

    #endregion

    #region GUI

    private void OnGUI()
    {
        DrawHeader();
        DrawTimeModeToggle();
        DrawMonthYearSelector();
        DrawWeekHeaders();
        DrawCalendar();
        DrawTimeSection();
        DrawConfirmButton();
    }

    private void DrawHeader()
    {
        GUILayout.Space(8);

        string mode = m_UseUTC ? "UTC" : "Local";
        EditorGUILayout.LabelField($"Select Date & Time ({mode})", EditorStyles.boldLabel);

        GUILayout.Space(6);
    }

    private void DrawTimeModeToggle()
    {
        EditorGUILayout.BeginHorizontal("box");

        GUILayout.Label("Mode:", GUILayout.Width(45));

        bool utc = GUILayout.Toggle(m_UseUTC, "UTC", GUILayout.Width(50));

        if (utc && !m_UseUTC)
        {
            m_UseUTC = true;
            EditorPrefs.SetBool(TIME_MODE_PREF_KEY, m_UseUTC);
            SetCurrentTime();
        }

        bool local = GUILayout.Toggle(!m_UseUTC, "Local", GUILayout.Width(60));
        if (local && m_UseUTC)
        {
            m_UseUTC = false;
            EditorPrefs.SetBool(TIME_MODE_PREF_KEY, m_UseUTC);
            SetCurrentTime();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Now", GUILayout.Width(50)))
        {
            SetCurrentTime();
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
    }

    private void DrawMonthYearSelector()
    {
        EditorGUILayout.BeginHorizontal("box");

        if (GUILayout.Button("◀", GUILayout.Width(30)))
        {
            m_SelectedMonth--;
            if (m_SelectedMonth < 1)
            {
                m_SelectedMonth = 12;
                m_SelectedYear--;
            }
        }

        GUILayout.FlexibleSpace();

        GUILayout.Label($"{(eMonthName)m_SelectedMonth} {m_SelectedYear}", EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("▶", GUILayout.Width(30)))
        {
            m_SelectedMonth++;
            if (m_SelectedMonth > 12)
            {
                m_SelectedMonth = 1;
                m_SelectedYear++;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawWeekHeaders()
    {
        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        string[] days = { "S", "M", "T", "W", "T", "F", "S" };

        GUIStyle centered = new GUIStyle(EditorStyles.label);
        centered.alignment = TextAnchor.MiddleCenter;

        GUILayout.FlexibleSpace();
        foreach (var d in days)
            GUILayout.Label(d, centered, GUILayout.Width(28));
        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCalendar()
    {
        GUILayout.Space(4);
        EditorGUILayout.BeginVertical("box");

        int daysInMonth = DateTime.DaysInMonth(m_SelectedYear, m_SelectedMonth);

        if (m_SelectedDay > daysInMonth)
            m_SelectedDay = daysInMonth;

        DateTime firstDay = new DateTime(m_SelectedYear, m_SelectedMonth, 1);
        int startOffset = (int)firstDay.DayOfWeek;

        int currentDay = 1;
        int totalCells = startOffset + daysInMonth;
        int rows = Mathf.CeilToInt(totalCells / 7f);

        for (int row = 0; row < rows; row++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int col = 0; col < 7; col++)
            {
                int cellIndex = row * 7 + col;

                if (cellIndex < startOffset || currentDay > daysInMonth)
                {
                    GUILayout.Space(28);
                }
                else
                {
                    GUIStyle style = new GUIStyle(GUI.skin.button);

                    if (currentDay == m_SelectedDay)
                    {
                        style.normal.textColor = Color.white;
                        style.fontStyle = FontStyle.Bold;
                        style.normal.background = selectedDayTexture;
                    }

                    if (GUILayout.Button(currentDay.ToString(), style, GUILayout.Width(28), GUILayout.Height(28)))
                        m_SelectedDay = currentDay;

                    currentDay++;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTimeSection()
    {
        GUILayout.Space(8);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Time (24h)", EditorStyles.miniBoldLabel);
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        DrawTimeField(ref m_SelectedHour, 0, 23);
        GUILayout.Label(":", GUILayout.Width(10));
        DrawTimeField(ref m_SelectedMinute, 0, 59);
        GUILayout.Label(":", GUILayout.Width(10));
        DrawTimeField(ref m_SelectedSeconds, 0, 59);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawConfirmButton()
    {
        GUILayout.Space(12);

        DateTimeKind kind = m_UseUTC ? DateTimeKind.Utc : DateTimeKind.Local;

        DateTime selected = new DateTime(
            m_SelectedYear,
            m_SelectedMonth,
            m_SelectedDay,
            m_SelectedHour,
            m_SelectedMinute,
            m_SelectedSeconds,
            kind
        );

        GUIStyle confirmStyle = new GUIStyle(GUI.skin.button);
        confirmStyle.fontSize = 12;
        confirmStyle.fixedHeight = 45;

        if (GUILayout.Button(
            "Confirm Selection\n" + selected.ToString("dd MMM yyyy  HH:mm:ss"),
            confirmStyle))
        {
            OnDatePicked?.Invoke(selected);
            Close();
        }

        GUILayout.Space(5);
    }

    #endregion

    #region Helpers

    private void DrawTimeField(ref int value, int min, int max)
    {
        string input = EditorGUILayout.TextField(
            value.ToString("00"),
            GUILayout.Width(60)
        );

        if (int.TryParse(input, out int parsed))
            value = Mathf.Clamp(parsed, min, max);

        value = Mathf.Clamp(value, min, max);
    }

    private void CreateTextures()
    {
        selectedDayTexture = MakeTex(2, 2, new Color(0.24f, 0.48f, 0.90f));
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    #endregion
}