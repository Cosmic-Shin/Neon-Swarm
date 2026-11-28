using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Tools > GD5 > Deploy Cloud Code: uploads + publishes Assets/CloudCode/SendMail.js using the editor's
/// Unity login (same admin API the Cloud Code package uses), so no service-account key is needed.</summary>
static class CloudCodeDeploy
{
    const string Script = "SendMail", Path = "Assets/CloudCode/SendMail.js";
    const string Params = "[{\"name\":\"to\",\"type\":\"STRING\",\"required\":true},{\"name\":\"text\",\"type\":\"STRING\",\"required\":true}]";

    [MenuItem("Tools/GD5/Deploy Cloud Code")]
    static async void Deploy()
    {
        string token = await new Unity.Services.Core.Editor.AccessTokens().GetServicesGatewayTokenAsync();
        // the editor often has no environment selected (all-zero id), so ask for the project's default one
        var (envStatus, envs) = await Send("GET", $"https://services.unity.com/api/unity/v1/projects/{CloudProjectSettings.projectId}/environments", null, token);
        var m = System.Text.RegularExpressions.Regex.Match(envs, "\"id\":\"([^\"]+)\"[^}]*\"isDefault\":true");
        string env = m.Success ? m.Groups[1].Value : null;
        if (string.IsNullOrEmpty(CloudProjectSettings.projectId) || env == null) { Debug.LogError("[CloudCode] project not linked or no environment"); return; }
        string root = $"https://services.unity.com/api/cloud-code/v1/projects/{CloudProjectSettings.projectId}/environments/{env}/scripts";
        string code = JsonEscape(System.IO.File.ReadAllText(Path));

        var (status, body) = await Send("PATCH", root + "/" + Script, $"{{\"code\":{code},\"params\":{Params}}}", token);
        if (status == 404)
            (status, body) = await Send("POST", root, $"{{\"name\":\"{Script}\",\"type\":\"API\",\"language\":\"JS\",\"code\":{code},\"params\":{Params}}}", token);
        if (status >= 300) { Debug.LogError($"[CloudCode] upload failed {status}: {body}"); return; }
        (status, body) = await Send("POST", root + "/" + Script + "/publish", "{}", token);
        if (status >= 300 && !body.Contains("9018")) { Debug.LogError($"[CloudCode] publish failed {status}: {body}"); return; }   // 9018 = already published
        Debug.Log($"[CloudCode] {Script} deployed to the default environment ({env})");
    }

    static async Task<(long, string)> Send(string method, string url, string json, string token)
    {
        using var req = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
        if (json != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + token);
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();
        return (req.responseCode, req.downloadHandler.text);
    }

    static string JsonEscape(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
            sb.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ => c < ' ' ? $"\\u{(int)c:x4}" : c.ToString() });
        return sb.Append('"').ToString();
    }
}
