using System;
using System.Collections.Generic;
using System.IO; 
using System.Text.Json.Nodes;
using PCL.Core.IO;

namespace PCL.Core.App;

public class CustomVarible
{
    /// <summary>
    /// 自定义主页变量保存路径。
    /// </summary>
    public static string VaribleJsonPath { get; } = Path.Combine(FileService.SharedDataPath, "varibles.json");
    
    /// <summary>
    /// 存放所有自定义主页变量的 JSON 对象。
    /// </summary>
    public static JsonNode? VaribleJson;

    public static Dictionary<string, string> VaribleDict = new();
    
    public static void Set(string key, string value)
    {
        
    }
    
    public static void Get(string key, string value)
    {
        
    }

    public static void Init()
    {
        if (!File.Exists(VaribleJsonPath))
        {
            File.Create(VaribleJsonPath).Close();
        }
        else
        {
            try
            {
                VaribleJson = JsonNode.Parse(File.ReadAllText(VaribleJsonPath));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}