using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Text.RegularExpressions;

public class Decryptor : EditorWindow
{
    private string inputText = "";
    private string outputText = "";
    private Vector2 inputScroll;
    private Vector2 outputScroll;

    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle textAreaStyle;
    private GUIStyle buttonStyle;

    [MenuItem("Sachin Saga Tools/ 9. Decryptor", priority = 9)]
    public static void ShowWindow()
    {
        GetWindow<Decryptor>("AES Decryptor");
    }

    private void InitStyles()
    {
        if (headerStyle != null) return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        boxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 10, 10)
        };

        textAreaStyle = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = true
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fixedHeight = 30
        };
    }

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.Space(10);
        GUILayout.Label("🔐 AES Decryptor", headerStyle);
        EditorGUILayout.Space(10);

        DrawInputSection();
        EditorGUILayout.Space(10);
        DrawButtons();
        EditorGUILayout.Space(10);
        DrawOutputSection();
    }

    private void DrawInputSection()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        GUILayout.Label("Encrypted Base64 Input", EditorStyles.boldLabel);

        inputScroll = EditorGUILayout.BeginScrollView(inputScroll, GUILayout.Height(120));
        inputText = EditorGUILayout.TextArea(inputText, textAreaStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔓 Decrypt", buttonStyle))
        {
            Decrypt();
        }

        if (GUILayout.Button("🧹 Clear", buttonStyle))
        {
            inputText = "";
            outputText = "";
        }

        if (GUILayout.Button("📋 Copy Output", buttonStyle))
        {
            EditorGUIUtility.systemCopyBuffer = outputText;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawOutputSection()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        GUILayout.Label("Decrypted Output (JSON)", EditorStyles.boldLabel);

        outputScroll = EditorGUILayout.BeginScrollView(outputScroll, GUILayout.Height(180));
        EditorGUILayout.TextArea(outputText, textAreaStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void Decrypt()
    {
        try
        {
            var encryptedBytes = Convert.FromBase64String(inputText.Trim());
            var decryptedBytes = AesEncryptor.Decrypt(encryptedBytes);
            var rawText = Encoding.UTF8.GetString(decryptedBytes);

            outputText = FormatJson(rawText);
        }
        catch (Exception e)
        {
            outputText = "❌ Error:\n" + e.Message;
        }
    }

    // 🔥 Lightweight JSON pretty printer (no external libs)
    private string FormatJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "";

        try
        {
            int indent = 0;
            bool quoted = false;
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];

                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;

                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent--;
                            sb.Append(new string(' ', indent * 2));
                        }
                        sb.Append(ch);
                        break;

                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        int index = i;
                        while (index > 0 && json[--index] == '\\')
                            escaped = !escaped;

                        if (!escaped)
                            quoted = !quoted;
                        break;

                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;

                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
        catch
        {
            return json; // fallback if not valid JSON
        }
    }
}