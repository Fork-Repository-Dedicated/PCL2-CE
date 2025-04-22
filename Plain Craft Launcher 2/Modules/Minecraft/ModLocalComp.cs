using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;

namespace PCL
{

    public static class ModLocalComp
    {
        private const int LocalModCacheVersion = 7;

        public class LocalCompFile
        {

            #region 基础

            /// <summary>
        /// 资源的文件的地址。
        /// </summary>
            public readonly string Path;
            public LocalCompFile(string Path)
            {
                this.Path = Path ?? "";
            }
            /// <summary>
        /// Mod 资源的完整路径，去除最后的 .disabled 和 .old。
        /// </summary>
            public string RawPath
            {
                get
                {
                    return ModBase.GetPathFromFullPath(Path) + RawFileName;
                }
            }

            /// <summary>
        /// 资源的完整文件名。
        /// </summary>
            public string FileName
            {
                get
                {
                    return ModBase.GetFileNameFromPath(Path);
                }
            }

            /// <summary>
        /// Mod 资源的完整文件名，去除最后的 .disabled 和 .old。
        /// </summary>
            public string RawFileName
            {
                get
                {
                    return FileName.Replace(".disabled", "").Replace(".old", "");
                }
            }

            /// <summary>
        /// 资源的状态。对于 Mod 有 Disabled
        /// </summary>
            public LocalFileStatus State
            {
                get
                {
                    Load();
                    if (!IsFileAvailable)
                    {
                        return LocalFileStatus.Unavailable;
                    }
                    else if (Path.EndsWithF(".disabled", true) || Path.EndsWithF(".old", true))
                    {
                        return LocalFileStatus.Disabled;
                    }
                    else
                    {
                        return LocalFileStatus.Fine;
                    }
                }
            }
            public enum LocalFileStatus : int
            {
                Fine = 0,
                Disabled = 1,
                Unavailable = 2
            }

            #endregion

            #region 信息项

            /// <summary>
        /// Mod 的名称。若不可用则为 ModID 或无扩展的文件名。
        /// </summary>
            public string Name
            {
                get
                {
                    if (_Name is null)
                        Load();
                    if (_Name is null)
                        _Name = _ModId;
                    if (_Name is null)
                        _Name = ModBase.GetFileNameWithoutExtentionFromPath(Path);
                    return _Name;
                }
                set
                {
                    if (_Name is null && value is not null && !value.Contains("modname") && value.ToLower() != "name" && value.Count() > 1 && (ModBase.Val(value).ToString() ?? "") != (value ?? ""))
                    {
                        _Name = value;
                    }
                }
            }
            private string _Name = null;

            /// <summary>
        /// Mod 的描述信息。
        /// </summary>
            public string Description
            {
                get
                {
                    if (_Description is null)
                        Load();
                    if (_Description is null && FileUnavailableReason is not null)
                        _Description = FileUnavailableReason.Message;
                    // If _Description Is Nothing Then _Description = Path
                    return _Description;
                }
                set
                {
                    if (_Description is null && value is not null && value.Count() > 2)
                    {
                        _Description = value.ToString().Trim(Conversions.ToChar(Constants.vbLf));
                        // 优化显示：若以 [a-zA-Z0-9] 结尾，加上小数点句号
                        if (_Description.ToLower().LastIndexOfAny("qwertyuiopasdfghjklzxcvbnm0123456789".ToCharArray()) == _Description.Count() - 1)
                            _Description += ".";
                    }
                }
            }
            private string _Description = null;

            /// <summary>
        /// Mod 的版本，不保证符合版本格式规范。
        /// </summary>
            public string Version
            {
                get
                {
                    if (_Version is null)
                        Load();
                    return _Version;
                }
                set
                {
                    if (_Version is not null && _Version.RegexCheck(@"[0-9.\-]+"))
                        return;
                    if (value?.ContainsF("version", true) == true)
                        value = "version"; // 需要修改的标识
                    _Version = value;
                }
            }
            public string _Version = null;

            /// <summary>
        /// 用于依赖检查的 ModID。
        /// </summary>
            public string ModId
            {
                get
                {
                    if (_ModId is null)
                        Load();
                    return _ModId;
                }
                set
                {
                    if (value is null)
                        return;
                    value = value.RegexSeek("[0-9a-zA-Z_-]+");
                    if (value is null || value.Count() <= 1 || (ModBase.Val(value).ToString() ?? "") == (value ?? ""))
                        return;
                    if (value.ContainsF("name", true) || value.ContainsF("modid", true))
                        return;
                    if (!PossibleModId.Contains(value))
                        PossibleModId.Add(value);
                    if (_ModId is null)
                        _ModId = value;
                }
            }
            private string _ModId = null;
            /// <summary>
        /// 其他可能的 ModID。
        /// </summary>
            public List<string> PossibleModId = new List<string>();

            /// <summary>
        /// Mod 的主页。
        /// </summary>
            public string Url
            {
                get
                {
                    if (_Url is null)
                        Load();
                    return _Url;
                }
                set
                {
                    if (_Url is null && value is not null && value.StartsWithF("http"))
                    {
                        _Url = value;
                    }
                }
            }
            private string _Url = null;

            /// <summary>
        /// Mod 的作者列表。
        /// </summary>
            public string Authors
            {
                get
                {
                    if (_Authors is null)
                        Load();
                    return _Authors;
                }
                set
                {
                    if (_Authors is null && !string.IsNullOrWhiteSpace(value))
                    {
                        _Authors = value;
                    }
                }
            }
            private string _Authors = null;

            /// <summary>
        /// 依赖项，其中包括了 Minecraft 的版本要求。格式为 ModID - VersionRequirement，若无版本要求则为 Nothing。
        /// </summary>
            public Dictionary<string, string> Dependencies
            {
                get
                {
                    Load();
                    return _Dependencies;
                }
            }
            private Dictionary<string, string> _Dependencies = new Dictionary<string, string>();
            private void AddDependency(string ModID, string VersionRequirement = null)
            {
                // 确保信息正确
                if (ModID is null || ModID.Count() < 2)
                    return;
                ModID = ModID.ToLower();
                if (ModID == "name" || (ModBase.Val(ModID).ToString() ?? "") == (ModID ?? ""))
                    return; // 跳过 name 与纯数字 id
                if (VersionRequirement is null || !VersionRequirement.Contains(".") && !VersionRequirement.Contains("-") || VersionRequirement.Contains("$"))
                {
                    VersionRequirement = null;
                }
                else if (!VersionRequirement.StartsWithF("[") && !VersionRequirement.StartsWithF("(") && !VersionRequirement.EndsWithF("]") && !VersionRequirement.EndsWithF(")"))
                    VersionRequirement = "[" + VersionRequirement + ",)";
                // 向依赖项中添加
                if (_Dependencies.ContainsKey(ModID))
                {
                    if (_Dependencies[ModID] is null)
                        _Dependencies[ModID] = VersionRequirement;
                }
                else
                {
                    _Dependencies.Add(ModID, VersionRequirement);
                }
            }

            #endregion

            #region 加载步骤标记

            // 1. 进行文件可用性检查
            // 成功：继续第二步。
            // 失败：标记 FileUnavailableReason， 并停止后续加载。
            /// <summary>
        /// 是否已进行 Mod 文件的基础加载。（这包括第一步和第二步）
        /// </summary>
            private bool IsLoaded = false;
            /// <summary>
        /// Mod 文件是否可被正常读取。
        /// </summary>
            public bool IsFileAvailable
            {
                get
                {
                    Load();
                    return FileUnavailableReason is null;
                }
            }
            /// <summary>
        /// Mod 文件出错的原因。若无错误，则为 Nothing。
        /// </summary>
            public Exception FileUnavailableReason
            {
                get
                {
                    Load();
                    return _FileUnavailableReason;
                }
            }
            private Exception _FileUnavailableReason = null;

            // 2. 进行 .class 以外的信息获取
            // 成功：标记 IsInfoWithoutClassAvailable。
            // 失败：什么也不干。如果需要补充信息的话，检测到 IsInfoWithoutClassAvailable 为 False，会自动继续加载。
            /// <summary>
        /// 是否已在不获取 .class 文件的前提下完成了所需信息的加载。
        /// </summary>
            private bool IsInfoWithoutClassAvailable = false;

            // 3. 尝试从 .class 文件中获取信息
            // 成功：标记 IsInfoWithClassAvailable。
            // 失败：什么也不干。
            /// <summary>
        /// 是否已进行 .class 文件的信息获取。
        /// </summary>
            private bool IsInfoWithClassLoaded = false;
            /// <summary>
        /// 是否已在 .class 文件中完成了所需信息的加载。
        /// </summary>
            private bool IsInfoWithClassAvailable = false;

            #endregion

            #region 加载

            /// <summary>
        /// 初始化所有数据。
        /// </summary>
            private void Init()
            {
                _Name = null;
                _Description = null;
                _Version = null;
                _ModId = null;
                PossibleModId = new List<string>();
                _Dependencies = new Dictionary<string, string>();
                IsLoaded = false;
                _FileUnavailableReason = null;
                IsInfoWithClassLoaded = false;
                IsInfoWithClassAvailable = false;
            }

            /// <summary>
        /// 进行文件可用性检查与 .class 以外的信息获取。
        /// </summary>
            public void Load(bool ForceReload = false)
            {
                if (IsLoaded && !ForceReload)
                    return;
                // 初始化
                Init();
                ZipArchive Jar = null;
                try
                {
                    // 基础可用性检查、打开 Jar 文件
                    if (Path.Length < 2)
                        throw new FileNotFoundException("错误的资源文件路径（" + (Path ?? "null") + "）");
                    if (!File.Exists(Path))
                        throw new FileNotFoundException("未找到资源文件（" + Path + "）");
                    Jar = new ZipArchive(new FileStream(Path, FileMode.Open));
                    // 信息获取
                    LookupMetadata(Jar);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ModBase.Log(ex, "资源文件由于无权限无法打开（" + Path + "）", ModBase.LogLevel.Developer);
                    _FileUnavailableReason = new UnauthorizedAccessException("没有读取此文件的权限，请尝试右键以管理员身份运行 PCL", ex);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "资源文件无法打开（" + Path + "）", ModBase.LogLevel.Developer);
                    _FileUnavailableReason = ex;
                }
                finally
                {
                    if (Jar is not null)
                        Jar.Dispose();
                }
                // 完成标记
                IsLoaded = true;
            }

            /// <summary>
        /// 从 Jar 文件中获取 Mod 信息。
        /// </summary>
            private void LookupMetadata(ZipArchive Jar)
            {
                #region 尝试使用 mcmod.info
                do
                {
                    try
                    {
                        // 获取信息文件
                        var InfoEntry = Jar.GetEntry("mcmod.info");
                        string InfoString = null;
                        if (InfoEntry is not null)
                        {
                            InfoString = ModBase.ReadFile(InfoEntry.Open());
                            if (InfoString.Length < 15)
                                InfoString = null;
                        }
                        if (InfoString is null)
                            break;
                        // 获取可用 Json 项
                        JObject InfoObject;
                        var JsonObject = ModBase.GetJson(InfoString);
                        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)JsonObject).Type, JTokenType.Array, false)))
                        {
                            InfoObject = (JObject)JsonObject(0);
                        }
                        else
                        {
                            InfoObject = (JObject)JsonObject("modList")(0);
                        }
                        // 从文件中获取 Mod 信息项
                        Name = (string)InfoObject["name"];
                        Description = (string)InfoObject["description"];
                        Version = (string)InfoObject["version"];
                        Url = (string)InfoObject["url"];
                        ModId = (string)InfoObject["modid"];
                        JArray AuthorJson = (JArray)InfoObject["authorList"];
                        if (AuthorJson is not null)
                        {
                            var Author = new List<string>();
                            foreach (var Token in AuthorJson)
                                Author.Add(Token.ToString());
                            if (Author.Any())
                                Authors = Author.Join(", ");
                        }
                        JArray Reqs = (JArray)InfoObject["requiredMods"];
                        if (Reqs is not null)
                        {
                            foreach (string Token in Reqs)
                            {
                                if (!string.IsNullOrEmpty(Token))
                                {
                                    Token = Token.Substring(Token.IndexOfF(":") + 1);
                                    if (Token.Contains("@"))
                                    {
                                        AddDependency(Token.Split("@")[0], Token.Split("@")[1]);
                                    }
                                    else
                                    {
                                        AddDependency(Token);
                                    }
                                }
                            }
                        }
                        Reqs = (JArray)InfoObject["dependancies"];
                        if (Reqs is not null)
                        {
                            foreach (string Token in Reqs)
                            {
                                if (!string.IsNullOrEmpty(Token))
                                {
                                    Token = Token.Substring(Token.IndexOfF(":") + 1);
                                    if (Token.Contains("@"))
                                    {
                                        AddDependency(Token.Split("@")[0], Token.Split("@")[1]);
                                    }
                                    else
                                    {
                                        AddDependency(Token);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取 mcmod.info 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
                    }
                }
                while (false);
                #endregion
                #region 尝试使用 fabric.mod.json
                do
                {
                    try
                    {
                        // 获取 fabric.mod.json 文件
                        var FabricEntry = Jar.GetEntry("fabric.mod.json");
                        string FabricText = null;
                        if (FabricEntry is not null)
                        {
                            FabricText = ModBase.ReadFile(FabricEntry.Open(), Encoding.UTF8);
                            if (!FabricText.Contains("schemaVersion"))
                                FabricText = null;
                        }
                        if (FabricText is null)
                            break;
                        JObject FabricObject = (JObject)ModBase.GetJson(FabricText);
                    GotFabric:
                        ;

                        // 从文件中获取 Mod 信息项
                        if (FabricObject.ContainsKey("name"))
                            Name = (string)FabricObject["name"];
                        if (FabricObject.ContainsKey("version"))
                            Version = (string)FabricObject["version"];
                        if (FabricObject.ContainsKey("description"))
                            Description = (string)FabricObject["description"];
                        if (FabricObject.ContainsKey("id"))
                            ModId = (string)FabricObject["id"];
                        if (FabricObject.ContainsKey("contact"))
                            Url = (string)(FabricObject["contact"]["homepage"] ?? "");
                        JArray AuthorJson = (JArray)FabricObject["authors"];
                        if (AuthorJson is not null)
                        {
                            var Author = new List<string>();
                            foreach (var Token in AuthorJson)
                                Author.Add(Token.ToString());
                            if (Author.Any())
                                Authors = Author.Join(", ");
                        }
                        // If (Not FabricObject.ContainsKey("serverSideOnly")) OrElse FabricObject("serverSideOnly")("value").ToObject(Of Boolean) = False Then
                        // '添加 Minecraft 依赖
                        // Dim DepMinecraft As String = If(If(FabricObject("acceptedMinecraftVersions") IsNot Nothing, FabricObject("acceptedMinecraftVersions")("value"), ""), "")
                        // If DepMinecraft <> "" Then AddDependency("minecraft", DepMinecraft)
                        // '添加其他依赖
                        // Dim Deps As String = If(If(FabricObject("dependencies") IsNot Nothing, FabricObject("dependencies")("value"), ""), "")
                        // If Deps <> "" Then
                        // For Each Dep In Deps.Split(";")
                        // If Dep = "" OrElse Not Dep.StartsWithF("required-") Then Continue For
                        // Dep = Dep.Substring(Dep.IndexOfF(":") + 1)
                        // If Dep.Contains("@") Then
                        // AddDependency(Dep.Split("@")(0), Dep.Split("@")(1))
                        // Else
                        // AddDependency(Dep)
                        // End If
                        // Next
                        // End If
                        // End If
                        // 加载成功
                        goto Finished;
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取 fabric.mod.json 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
                    }
                }
                while (false);
                #endregion
                #region 尝试使用 mods.toml
                do
                {
                    try
                    {
                        // 获取 mods.toml 文件
                        var TomlEntry = Jar.GetEntry("META-INF/mods.toml");
                        string TomlText = null;
                        if (TomlEntry is not null)
                        {
                            TomlText = ModBase.ReadFile(TomlEntry.Open());
                            if (TomlText.Length < 15)
                                TomlText = null;
                        }
                        if (TomlText is null)
                            break;
                        // 文件标准化：统一换行符为 vbLf，去除注释、头尾的空格、空行
                        var Lines = new List<string>();
                        foreach (var Line in TomlText.Replace(Constants.vbCrLf, Constants.vbLf).Replace(Constants.vbCr, Constants.vbLf).Split(Constants.vbLf)) // 统一换行符
                        {
                            if (Line.StartsWithF("#")) // 去除注释
                            {
                                continue;
                            }
                            else if (Line.Contains("#"))
                            {
                                Line = Line.Substring(0, Line.IndexOfF("#"));
                            }
                            Line = Line.Trim(new char[] { ' ', '\t', '　' }); // 去除头尾的空格
                            if (Line.Any())
                                Lines.Add(Line); // 去除空行
                        }
                        // 读取文件数据
                        var TomlData = new List<KeyValuePair<string, Dictionary<string, object>>>() { new KeyValuePair<string, Dictionary<string, object>>("", new Dictionary<string, object>()) };
                        bool exitTry = false;
                        for (int i = 0, loopTo = Lines.Count - 1; i <= loopTo; i++)
                        {
                            string Line = Lines[i];
                            if (Line.StartsWithF("[") && Line.EndsWithF("]"))
                            {
                                // 段落标记
                                string Header = Line.Trim("[]".ToCharArray());
                                TomlData.Add(new KeyValuePair<string, Dictionary<string, object>>(Header, new Dictionary<string, object>()));
                            }
                            else if (Line.Contains("="))
                            {
                                // 字段标记
                                string Key = Line.Substring(0, Line.IndexOfF("=")).TrimEnd(new char[] { ' ', '\t', '　' });
                                string RawValue = Line.Substring(Line.IndexOfF("=") + 1).TrimStart(new char[] { ' ', '\t', '　' });
                                object Value;
                                if (RawValue.StartsWithF("\"") && RawValue.EndsWithF("\""))
                                {
                                    // 单行字符串
                                    Value = RawValue.Trim('"');
                                }
                                else if (RawValue.StartsWithF("'''"))
                                {
                                    // 多行字符串
                                    var ValueLines = new List<string>() { RawValue.TrimStart('\'') };
                                    if (ValueLines[0].EndsWithF("'''")) // 把多行字符串按单行写法写的错误处理（#2732）
                                    {
                                        ValueLines[0] = ValueLines[0].TrimEnd('\'');
                                    }
                                    else
                                    {
                                        while (i < Lines.Count - 1)
                                        {
                                            i += 1;
                                            string ValueLine = Lines[i];
                                            if (ValueLine.EndsWithF("'''"))
                                            {
                                                ValueLines.Add(ValueLine.TrimEnd('\''));
                                                break;
                                            }
                                            else
                                            {
                                                ValueLines.Add(ValueLine);
                                            }
                                        }
                                    }
                                    Value = ValueLines.Join(Constants.vbLf).Trim(Conversions.ToChar(Constants.vbLf)).Replace(Constants.vbLf, Constants.vbCrLf);
                                }
                                else if (RawValue.ToLower() == "true" || RawValue.ToLower() == "false")
                                {
                                    // 布尔型
                                    Value = RawValue.ToLower() == "true";
                                }
                                else if ((ModBase.Val(RawValue).ToString() ?? "") == (RawValue ?? ""))
                                {
                                    // 数字型
                                    Value = ModBase.Val(RawValue);
                                }
                                else
                                {
                                    // 不知道是个啥玩意儿，直接存储
                                    Value = RawValue;
                                }
                                TomlData.Last().Value[Key] = Value;
                            }
                            else
                            {
                                // 不知道是个啥玩意儿
                                exitTry = true;
                                break;
                            }
                        }

                        if (exitTry)
                        {
                            break;
                        }
                        // 从文件数据中获取信息
                        Dictionary<string, object> ModEntry = null;
                        foreach (var TomlSubData in TomlData)
                        {
                            if (TomlSubData.Key == "mods")
                            {
                                ModEntry = TomlSubData.Value;
                                break;
                            }
                        }
                        if (ModEntry is null || !ModEntry.ContainsKey("modId"))
                            break;
                        ModId = Conversions.ToString(ModEntry["modId"]);
                        if (_ModId is null)
                            break; // 设置了无效的 ModID
                        if (ModEntry.ContainsKey("displayName"))
                            Name = Conversions.ToString(ModEntry["displayName"]);
                        if (ModEntry.ContainsKey("description"))
                            Description = Conversions.ToString(ModEntry["description"]);
                        if (ModEntry.ContainsKey("version"))
                            Version = Conversions.ToString(ModEntry["version"]);
                        if (TomlData[0].Value.ContainsKey("displayURL"))
                            Url = Conversions.ToString(TomlData[0].Value["displayURL"]);
                        if (TomlData[0].Value.ContainsKey("authors"))
                            Authors = Conversions.ToString(TomlData[0].Value["authors"]);
                        foreach (var TomlSubData in TomlData)
                        {
                            if ((TomlSubData.Key.ToLower() ?? "") == ($"dependencies.{ModId.ToLower()}" ?? ""))
                            {
                                var DepEntry = TomlSubData.Value;
                                if (Conversions.ToBoolean(DepEntry.ContainsKey("modId") && DepEntry.ContainsKey("mandatory") && DepEntry["mandatory"] && DepEntry.ContainsKey("side") && !(DepEntry["side"].ToString().ToLower() == "server")))
                                {
                                    AddDependency(Conversions.ToString(DepEntry["modId"]), Conversions.ToString(DepEntry.ContainsKey("versionRange") ? DepEntry["versionRange"] : null));
                                }
                            }
                        }
                        // 加载成功
                        goto Finished;
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取 mods.toml 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
                    }
                }
                while (false);
                #endregion
                #region 尝试使用 fml_cache_annotation.json
                do
                {
                    try
                    {
                        // 获取 fml_cache_annotation.json 文件
                        var FmlEntry = Jar.GetEntry("META-INF/fml_cache_annotation.json");
                        string FmlText = null;
                        if (FmlEntry is not null)
                        {
                            FmlText = ModBase.ReadFile(FmlEntry.Open(), Encoding.UTF8);
                            if (!FmlText.Contains("Lnet/minecraftforge/fml/common/Mod;"))
                                FmlText = null;
                        }
                        if (FmlText is null)
                            break;
                        JObject FmlJson = (JObject)ModBase.GetJson(FmlText);
                        // 获取可用 Json 项
                        JObject FmlObject = null;
                        foreach (KeyValuePair<string, JToken> ModFilePair in FmlJson)
                        {
                            JArray ModFileAnnos = (JArray)ModFilePair.Value["annotations"];
                            if (ModFileAnnos is not null)
                            {
                                // 先获取 Mod
                                foreach (var ModFileAnno in ModFileAnnos)
                                {
                                    string Name = (string)(ModFileAnno["name"] ?? "");
                                    if (Name == "Lnet/minecraftforge/fml/common/Mod;")
                                    {
                                        FmlObject = (JObject)ModFileAnno["values"];
                                        goto Got;
                                    }
                                }
                            }
                        }
                        break;
                    Got:
                        ;

                        // 从文件中获取 Mod 信息项
                        if (FmlObject.ContainsKey("useMetadata") && (FmlObject["useMetadata"]["value"] ?? "").ToString().ToLower() == "true")
                        {
                            // 要求使用 mcmod.info 中的信息
                            string value = (string)FmlObject["modid"]["value"];
                            if (value is null)
                                break;
                            value = value.ToLower().RegexSeek("[0-9a-z_]+");
                            if (value is not null && value.ToLower() != "name" && value.Count() > 1 && (ModBase.Val(value).ToString() ?? "") != (value ?? ""))
                            {
                                if (!PossibleModId.Contains(value))
                                    PossibleModId.Add(value);
                            }
                            break;
                        }
                        if (FmlObject.ContainsKey("name"))
                            Name = (string)FmlObject["name"]["value"];
                        if (FmlObject.ContainsKey("version"))
                            Version = (string)FmlObject["version"]["value"];
                        if (FmlObject.ContainsKey("modid"))
                            ModId = (string)FmlObject["modid"]["value"];
                        if (!FmlObject.ContainsKey("serverSideOnly") || FmlObject["serverSideOnly"]["value"].ToObject<bool>() == false)
                        {
                            // 添加 Minecraft 依赖
                            string DepMinecraft = (string)((FmlObject["acceptedMinecraftVersions"] is not null ? FmlObject["acceptedMinecraftVersions"]["value"] : "") ?? "");
                            if (!string.IsNullOrEmpty(DepMinecraft))
                                AddDependency("minecraft", DepMinecraft);
                            // 添加其他依赖
                            string Deps = (string)((FmlObject["dependencies"] is not null ? FmlObject["dependencies"]["value"] : "") ?? "");
                            if (!string.IsNullOrEmpty(Deps))
                            {
                                foreach (var Dep in Deps.Split(";"))
                                {
                                    if (string.IsNullOrEmpty(Dep) || !Dep.StartsWithF("required-"))
                                        continue;
                                    Dep = Dep.Substring(Dep.IndexOfF(":") + 1);
                                    if (Dep.Contains("@"))
                                    {
                                        AddDependency(Dep.Split("@")[0], Dep.Split("@")[1]);
                                    }
                                    else
                                    {
                                        AddDependency(Dep);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "读取 fml_cache_annotation.json 时出现未知错误（" + Path + "）", ModBase.LogLevel.Developer);
                    }
                }
                while (false);
            #endregion
            Finished:
                ;

                #region 将 Version 代号转换为 META-INF 中的版本
                if (_Version == "version")
                {
                    try
                    {
                        var MetaEntry = Jar.GetEntry("META-INF/MANIFEST.MF");
                        if (MetaEntry is not null)
                        {
                            string MetaString = ModBase.ReadFile(MetaEntry.Open()).Replace(" :", ":").Replace(": ", ":");
                            if (MetaString.Contains("Implementation-Version:"))
                            {
                                MetaString = MetaString.Substring(MetaString.IndexOfF("Implementation-Version:") + "Implementation-Version:".Count());
                                MetaString = MetaString.Substring(0, MetaString.IndexOfAny(Constants.vbCrLf.ToCharArray())).Trim();
                                Version = MetaString;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log("获取 META-INF 中的版本信息失败（" + Path + "）", ModBase.LogLevel.Developer);
                        Version = null;
                    }
                }
                if (_Version is not null && !(_Version.Contains(".") || _Version.Contains("-")))
                    Version = null;
                #endregion
            }

            #endregion

            #region 网络信息

            /// <summary>
        /// 当任何网络信息更新时触发。
        /// </summary>
            public event OnCompUpdateEventHandler OnCompUpdate;

            public delegate void OnCompUpdateEventHandler(LocalCompFile sender);

            /// <summary>
        /// 该 Mod 关联的网络项目。
        /// </summary>
            public ModComp.CompProject Comp
            {
                get
                {
                    return _Comp;
                }
                set
                {
                    _Comp = value;
                    OnCompUpdate?.Invoke(this);
                }
            }
            private ModComp.CompProject _Comp;

            /// <summary>
        /// 本地文件对应的联网文件信息。
        /// </summary>
            public ModComp.CompFile CompFile;

            /// <summary>
        /// 该 Mod 对应的联网最新版本。
        /// </summary>
            public ModComp.CompFile UpdateFile
            {
                get
                {
                    return _UpdateFile;
                }
                set
                {
                    _UpdateFile = value;
                    OnCompUpdate?.Invoke(this);
                }
            }
            private ModComp.CompFile _UpdateFile;

            /// <summary>
        /// 该 Mod 的更新日志网址。
        /// </summary>
            public List<string> ChangelogUrls = new List<string>();
            /// <summary>
        /// 所有网络信息是否已成功加载。
        /// </summary>
            public bool CompLoaded = false;

            /// <summary>
        /// 将网络信息保存为 Json。
        /// </summary>
            public JObject ToJson()
            {
                var Json = new JObject();
                if (Comp is not null)
                    Json.Add("Comp", Comp.ToJson());
                Json.Add("ChangelogUrls", new JArray(ChangelogUrls));
                Json.Add("CompLoaded", CompLoaded);
                if (CompFile is not null)
                    Json.Add("CompFile", CompFile.ToJson());
                if (UpdateFile is not null)
                    Json.Add("UpdateFile", UpdateFile.ToJson());
                return Json;
            }
            /// <summary>
        /// 从 Json 中读取网络信息。
        /// </summary>
            public void FromJson(JObject Json)
            {
                CompLoaded = (bool)Json["CompLoaded"];
                if (Json.ContainsKey("Comp"))
                    Comp = new ModComp.CompProject((JObject)Json["Comp"]);
                if (Json.ContainsKey("ChangelogUrls"))
                    ChangelogUrls = Json["ChangelogUrls"].ToObject<List<string>>();
                if (Json.ContainsKey("CompFile"))
                    CompFile = new ModComp.CompFile((JObject)Json["CompFile"], ModComp.CompType.Mod);
                if (Json.ContainsKey("UpdateFile"))
                    UpdateFile = new ModComp.CompFile((JObject)Json["UpdateFile"], ModComp.CompType.Mod);
            }

            /// <summary>
        /// 该文件是否可以更新。
        /// </summary>
            public bool CanUpdate
            {
                get
                {
                    return Conversions.ToBoolean(!ModBase.Setup.Get("UiHiddenFunctionModUpdate") && ChangelogUrls.Any());
                }
            }

            /// <summary>
        /// 获取用于 CurseForge 信息获取的 Hash 值（MurmurHash2）。
        /// </summary>
            public uint CurseForgeHash
            {
                get
                {
                    if (_CurseForgeHash is null)
                    {
                        // 读取缓存
                        var Info = new FileInfo(Path);
                        string CacheKey = ModBase.GetHash($"{RawPath}-{Info.LastWriteTime.ToLongTimeString()}-{Info.Length}-C").ToString();
                        string Cached = ModBase.ReadIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey);
                        if (!string.IsNullOrEmpty(Cached) && Cached.RegexCheck(@"^\d+$")) // #5062
                        {
                            _CurseForgeHash = Conversions.ToUInteger(Cached);
                            return (uint)_CurseForgeHash;
                        }
                        // 读取文件
                        var data = new List<byte>();
                        foreach (byte b in ModBase.ReadFileBytes(Path))
                        {
                            if (b == 9 || b == 10 || b == 13 || b == 32)
                                continue;
                            data.Add(b);
                        }
                        // 计算 MurmurHash2
                        int length = data.Count;
                        uint h = (uint)(1 ^ length); // 1 是种子
                        int i;
                        var loopTo = length - 4;
                        for (i = 0; i <= loopTo; i += 4)
                        {
                            uint k = data[i] | (uint)data[i + 1] << 8 | (uint)data[i + 2] << 16 | (uint)data[i + 3] << 24;
                            k = (uint)(k * 0x5BD1E995L & 0xFFFFFFFFL);
                            k = k ^ k >> 24;
                            k = (uint)(k * 0x5BD1E995L & 0xFFFFFFFFL);
                            h = (uint)(h * 0x5BD1E995L & 0xFFFFFFFFL);
                            h = h ^ k;
                        }
                        switch (length - i)
                        {
                            case 3:
                                {
                                    h = h ^ (data[i] | (uint)data[i + 1] << 8);
                                    h = h ^ (uint)data[i + 2] << 16;
                                    h = (uint)(h * 0x5BD1E995L & 0xFFFFFFFFL);
                                    break;
                                }
                            case 2:
                                {
                                    h = h ^ (data[i] | (uint)data[i + 1] << 8);
                                    h = (uint)(h * 0x5BD1E995L & 0xFFFFFFFFL);
                                    break;
                                }
                            case 1:
                                {
                                    h = h ^ data[i];
                                    h = (uint)(h * 0x5BD1E995L & 0xFFFFFFFFL);
                                    break;
                                }
                        }
                        h = h ^ h >> 13;
                        h = (uint)(h * 0x5BD1E995L & 0xFFFFFFFFL);
                        h = h ^ h >> 15;
                        _CurseForgeHash = h;
                        // 写入缓存
                        ModBase.WriteIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey, h.ToString());
                    }
                    return (uint)_CurseForgeHash;
                }
            }
            private uint? _CurseForgeHash;

            /// <summary>
        /// 获取用于 Modrinth 信息获取的 Hash 值（SHA1）。
        /// </summary>
            public string ModrinthHash
            {
                get
                {
                    if (_ModrinthHash is null)
                    {
                        // 读取缓存
                        var Info = new FileInfo(Path);
                        string CacheKey = ModBase.GetHash($"{RawPath}-{Info.LastWriteTime.ToLongTimeString()}-{Info.Length}-M").ToString();
                        string Cached = ModBase.ReadIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey);
                        if (!string.IsNullOrEmpty(Cached))
                        {
                            _ModrinthHash = Cached;
                            return _ModrinthHash;
                        }
                        // 计算 SHA1
                        _ModrinthHash = ModBase.GetFileSHA1(Path);
                        // 写入缓存
                        ModBase.WriteIni(ModBase.PathTemp + @"Cache\CompHash.ini", CacheKey, _ModrinthHash);
                    }
                    return _ModrinthHash;
                }
            }
            private string _ModrinthHash;

            #endregion

            #region API

            public override string ToString()
            {
                return $"{State} - {Path}";
            }
            public override bool Equals(object obj)
            {
                LocalCompFile target = obj as LocalCompFile;
                return target is not null && (Path ?? "") == (target.Path ?? "");
            }

            #endregion

            /// <summary>
        /// 是否可能为前置 Mod。
        /// </summary>
            public bool IsPresetMod()
            {
                return !Dependencies.Any() && Name is not null && (Name.ToLower().Contains("core") || Name.ToLower().Contains("lib"));
            }

            /// <summary>
        /// 根据完整文件路径的文件扩展名判断是否为 Mod 文件。
        /// </summary>
            public static object IsModFile(string Path)
            {
                if (Path is null || !Path.Contains("."))
                    return false;
                Path = Path.ToLower();
                if (Path.EndsWithF(".jar", true) || Path.EndsWithF(".zip", true) || Path.EndsWithF(".litemod", true) || Path.EndsWithF(".jar.disabled", true) || Path.EndsWithF(".zip.disabled", true) || Path.EndsWithF(".litemod.disabled", true) || Path.EndsWithF(".jar.old", true) || Path.EndsWithF(".zip.old", true) || Path.EndsWithF(".litemod.old", true))
                    return true;
                return false;
            }

        }

        public class CompLocalLoaderData
        {
            public ModMinecraft.McVersion GameVersion;
            public List<ModComp.CompLoaderType> Loaders;
            public PageVersionCompResource Frm;
            public string CompPath;

            public KeyValuePair<List<LocalCompFile>, JObject> DetailInfo;
        }

        // 加载资源列表
        public static ModLoader.LoaderTask<CompLocalLoaderData, List<LocalCompFile>> CompResourceListLoader = new ModLoader.LoaderTask<CompLocalLoaderData, List<LocalCompFile>>("Comp Resource List Loader", CompResourceListLoad);
        private static void CompResourceListLoad(ModLoader.LoaderTask<CompLocalLoaderData, List<LocalCompFile>> Loader)
        {
            try
            {
                ModBase.RunInUiWait(() => { if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.ShowProgress = false; });

                // 等待 Mod 更新完成
                if (PageVersionCompResource.UpdatingVersions.Contains(Loader.Input.CompPath))
                {
                    ModBase.Log($"[Mod] 等待资源更新完成后才能继续加载资源列表：" + Loader.Input.CompPath);
                    try
                    {
                        ModBase.RunInUiWait(() => { if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.Text = "正在更新资源"; });
                        while (PageVersionCompResource.UpdatingVersions.Contains(Loader.Input.CompPath))
                        {
                            if (Loader.IsAborted)
                                return;
                            Thread.Sleep(100);
                        }
                    }
                    finally
                    {
                        ModBase.RunInUiWait(() => { if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.Text = "正在加载资源列表"; });
                    }
                    Loader.Input.Frm.LoaderRun(ModLoader.LoaderFolderRunType.UpdateOnly);
                }

                // 获取 Mod 文件夹下的可用文件列表
                var ModFileList = new List<FileInfo>();
                if (Directory.Exists(Loader.Input.CompPath))
                {
                    string RawName = Loader.Input.CompPath.ToLower();
                    foreach (FileInfo File in ModBase.EnumerateFiles(Loader.Input.CompPath))
                    {
                        if ((File.DirectoryName.ToLower() + @"\" ?? "") != (RawName ?? ""))
                        {
                            // 仅当 Forge 1.13- 且文件夹名与版本号相同时，才加载该子文件夹下的 Mod
                            if (!(PageVersionLeft.Version is not null && PageVersionLeft.Version.Version.HasForge && PageVersionLeft.Version.Version.McCodeMain < 13 && (File.Directory.Name ?? "") == ("1." + PageVersionLeft.Version.Version.McCodeMain + "." + PageVersionLeft.Version.Version.McCodeSub ?? "")))
                            {
                                continue;
                            }
                        }
                        if (Conversions.ToBoolean(LocalCompFile.IsModFile(File.FullName)))
                            ModFileList.Add(File);
                    }
                }

                // 确定是否显示进度
                Loader.Progress = 0.05d;
                if (ModFileList.Count > 50)
                {
                    ModBase.RunInUi(() => { if (Loader.Input.Frm is not null) Loader.Input.Frm.Load.ShowProgress = true; });
                }

                // 获取本地文件缓存
                string CachePath = ModBase.PathTemp + @"Cache\LocalComp.json";
                var Cache = new JObject();
                try
                {
                    string CacheContent = ModBase.ReadFile(CachePath);
                    if (!string.IsNullOrWhiteSpace(CacheContent))
                    {
                        Cache = (JObject)ModBase.GetJson(CacheContent);
                        if (!Cache.ContainsKey("version") || Cache["version"].ToObject<int>() != LocalModCacheVersion)
                        {
                            ModBase.Log($"[Mod] 本地 Mod 信息缓存版本已过期，将弃用这些缓存信息", ModBase.LogLevel.Debug);
                            Cache = new JObject();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "读取本地 Mod 信息缓存失败，已重置");
                    Cache = new JObject();
                }
                Cache["version"] = LocalModCacheVersion;

                // 加载 Mod 列表
                var ModList = new List<LocalCompFile>();
                var ModUpdateList = new List<LocalCompFile>();
                foreach (FileInfo ModFile in ModFileList)
                {
                    Loader.Progress += 0.94d / ModFileList.Count;
                    if (Loader.IsAborted)
                        return;
                    // 加载 McMod 对象
                    var ModEntry = new LocalCompFile(ModFile.FullName);
                    ModEntry.Load();
                    var DumpMod = ModList.FirstOrDefault(m => (m.RawFileName ?? "") == (ModEntry.RawFileName ?? ""));
                    if (DumpMod is not null)
                    {
                        var DisabledMod = DumpMod.State == LocalCompFile.LocalFileStatus.Disabled ? DumpMod : ModEntry;
                        ModBase.Log($"[Mod] 重复的 Mod 文件：{DumpMod.FileName} 与 {ModEntry.FileName}，已忽略 {DisabledMod.FileName}", ModBase.LogLevel.Debug);
                        if (ReferenceEquals(DisabledMod, ModEntry))
                        {
                            continue;
                        }
                        else
                        {
                            ModList.Remove(DisabledMod);
                            ModUpdateList.Remove(DisabledMod);
                        }
                    }
                    ModList.Add(ModEntry);
                    // 读取 Comp 缓存
                    if (ModEntry.State == LocalCompFile.LocalFileStatus.Unavailable)
                        continue;
                    string CacheKey = ModEntry.ModrinthHash + Loader.Input.GameVersion.Version.McName + Loader.Input.Loaders.Join("");
                    if (Cache.ContainsKey(CacheKey))
                    {
                        ModEntry.FromJson((JObject)Cache[CacheKey]);
                        // 如果缓存中的信息在 6 小时以内更新过，则无需重新获取
                        if (ModEntry.CompLoaded && DateTime.Now - Cache[CacheKey]["Comp"]["CacheTime"].ToObject<DateTime>() < new TimeSpan(6, 0, 0))
                            continue;
                    }
                    ModUpdateList.Add(ModEntry);
                }
                Loader.Progress = 0.99d;
                ModBase.Log($"[Mod] 共有 {ModList.Count} 个 Mod，其中 {ModUpdateList.Where(m => m.Comp is null).Count()} 个需要联网获取信息，{ModUpdateList.Where(m => m.Comp is not null).Count()} 个需要更新信息");

                // 排序
                ModList = ModBase.Sort(ModList, (Left, Right) => { if (Left.State == LocalCompFile.LocalFileStatus.Unavailable != (Right.State == LocalCompFile.LocalFileStatus.Unavailable)) { return Left.State == LocalCompFile.LocalFileStatus.Unavailable; } else { return Conversions.ToBoolean(~Right.FileName.CompareTo(Left.FileName)); } });

                // 回设
                if (Loader.IsAborted)
                    return;
                Loader.Output = ModList;

                // 开始联网加载
                if (ModUpdateList.Any())
                {
                    // TODO: 添加信息获取中提示
                    Loader.Input.DetailInfo = new KeyValuePair<List<LocalCompFile>, JObject>(ModUpdateList, Cache);
                    CompUpdateDetailLoader.Start(Loader.Input, IsForceRestart: true);
                }
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "Mod 列表加载失败", ModBase.LogLevel.Debug);
                throw;
            }
        }
        // 联网加载 Mod 详情
        public static ModLoader.LoaderTask<CompLocalLoaderData, int> CompUpdateDetailLoader = new ModLoader.LoaderTask<CompLocalLoaderData, int>("Comp List Detail Loader", CompUpdateDetailLoad);
        private static void CompUpdateDetailLoad(ModLoader.LoaderTask<CompLocalLoaderData, int> Loader)
        {
            var Mods = Loader.Input.DetailInfo.Key;
            var Cache = Loader.Input.DetailInfo.Value;
            // 获取作为检查目标的加载器和版本
            // 此处不应向下扩展检查的 MC 小版本，例如 Mod 在更新 1.16.5 后，对早期的 1.16.2 版本发布了修补补丁，这会导致 PCL 将 1.16.5 版本的 Mod 降级到 1.16.2
            var ModLoaders = Loader.Input.Loaders;
            string McVersion = Loader.Input.GameVersion.Version.McName;
            // 开始网络获取
            ModBase.Log($"[Mod] 目标加载器：{ModLoaders.Join("/")}，版本：{McVersion}");
            int EndedThreadCount = 0;
            bool IsFailed = false;
            var MainThread = Thread.CurrentThread;
            // 从 Modrinth 获取信息
            ModBase.RunInNewThread(() =>
    {
        // 步骤 1：获取 Hash 与对应的工程 ID
        try
        {
            ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
            /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
               在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
               在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
            --- 引发异常的上一位置中堆栈跟踪的末尾 ---
               在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
               在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
            --- 引发异常的上一位置中堆栈跟踪的末尾 ---
               在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
               在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
            --- 引发异常的上一位置中堆栈跟踪的末尾 ---
               在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
               在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
            --- 引发异常的上一位置中堆栈跟踪的末尾 ---
               在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
               在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

            Input:
                                '步骤 1：获取 Hash 与对应的工程 ID
                                Dim ModrinthHashes = Mods.Select(Function(m) m.ModrinthHash).ToList()

             */
            ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
            /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                Input:
                                                    Dim ModrinthVersion = CType(Global.PCL.ModBase.GetJson(Global.PCL.ModDownload.DlModRequest("https://api.modrinth.com/v2/version_files", "POST",
                                                        $"{{""hashes"": [""{ModrinthHashes.[Join](""",""")}""], ""algorithm"": ""sha1""}}", "application/json")), Global.Newtonsoft.Json.Linq.JObject)

                                 */
            ModBase.Log($"[Mod] 从 Modrinth 获取到 {ModrinthVersion.Count} 个本地 Mod 的对应信息");
            // 步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
            if (ModrinthVersion.Count == 0)
                return;
            ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
            /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                Input:
                                                    Dim ModrinthMapping As New Global.System.Collections.Generic.Dictionary(Of String, Global.System.Collections.Generic.List(Of Global.PCL.ModLocalComp.LocalCompFile))

                                 */
            foreach (var Entry in Mods)
            {
                if (!ModrinthVersion.ContainsKey(Entry.ModrinthHash))
                    continue;
                if ((string)ModrinthVersion(Entry.ModrinthHash)("files")(0)("hashes")("sha1") != Entry.ModrinthHash)
                    continue;
                string ProjectId = ModrinthVersion(Entry.ModrinthHash)("project_id").ToString;
                if (ModComp.CompProjectCache.ContainsKey(ProjectId) && Entry.Comp is null)
                    Entry.Comp = ModComp.CompProjectCache(ProjectId); // 读取已加载的缓存，加快结果出现速度
                if (!ModrinthMapping.ContainsKey(ProjectId))
                    ModrinthMapping(ProjectId) = new List<LocalCompFile>();
                // 记录对应的 CompFile
                ModrinthMapping(ProjectId).Add(Entry);
                ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
                /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                           在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                           在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                        Input:
                                                                '记录对应的 CompFile
                                                                Dim File As New Global.PCL.ModComp.CompFile(ModrinthVersion(Entry.ModrinthHash), Global.PCL.ModComp.CompType.[Mod])

                                         */
                if (Entry.CompFile is null || Entry.CompFile.ReleaseDate < File.ReleaseDate)
                    Entry.CompFile = File;
            }
            if (Loader.IsAbortedWithThread(MainThread))
                return;
            ModBase.Log($"[Mod] 需要从 Modrinth 获取 {ModrinthMapping.Count} 个本地 Mod 的工程信息");
            // 步骤 3：获取工程信息
            if (!ModrinthMapping.Any())
                return;
            ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
            /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                Input:
                                                    Dim ModrinthProject = CType(Global.PCL.ModBase.GetJson(Global.PCL.ModDownload.DlModRequest(
                                                        $"https://api.modrinth.com/v2/projects?ids=[""{ModrinthMapping.Keys.[Join](""",""")}""]",
                                                        "GET", "", "application/json")), Global.Newtonsoft.Json.Linq.JArray)

                                 */
            foreach (var ProjectJson in ModrinthProject)
            {
                ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
                /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                           在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                           在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                        Input:
                                                                Dim Project As New Global.PCL.ModComp.CompProject(ProjectJson)

                                         */
                foreach (var Entry in ModrinthMapping(Project.Id))
                    Entry.Comp = Project;
            }
            // 步骤 4：获取更新信息
            ModBase.Log($"[Mod] 已从 Modrinth 获取本地 Mod 信息，继续获取更新信息");
            ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
            /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                   在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                   在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                   在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                Input:
                                                    '步骤 4：获取更新信息
                                                    Dim ModrinthUpdate = CType(Global.PCL.ModBase.GetJson(Global.PCL.ModDownload.DlModRequest("https://api.modrinth.com/v2/version_files/update", "POST",
                                                        $"{{""hashes"": [""{Global.System.Linq.Enumerable.SelectMany(OfGlobal.System.String)((CType((ModrinthMapping),Global.System.Collections.Generic.IEnumerable(OfGlobal.System.Collections.Generic.KeyValuePair(OfSystem.String,Global.System.Collections.Generic.List(OfGlobal.PCL.ModLocalComp.LocalCompFile))))),(Function(l) Global.System.Linq.Enumerable.[Select](OfGlobal.System.String)((CType((l.Value),Global.System.Collections.Generic.IEnumerable(OfGlobal.PCL.ModLocalComp.LocalCompFile))),(CType(((Function(m) m.ModrinthHash)),Global.System.Func(OfGlobal.PCL.ModLocalComp.LocalCompFile,System.String)))))).[Join](""",""")}""], ""algorithm"": ""sha1"", 
                                                    ""loaders"": [""{ModLoaders.[Join](""",""").ToLower}""],""game_versions"": [""{McVersion}""]}}", "application/json")), Global.Newtonsoft.Json.Linq.JObject)

                                 */
            foreach (var Entry in Mods)
            {
                if (!ModrinthUpdate.ContainsKey(Entry.ModrinthHash) || Entry.CompFile is null)
                    continue;
                ;
#error Cannot convert LocalDeclarationStatementSyntax - see comment for details
                /* Cannot convert LocalDeclarationStatementSyntax, System.NullReferenceException: 未将对象引用设置到对象的实例。
                                           在 ICSharpCode.CodeConverter.CSharp.CommonConversions.ShouldPreferExplicitType(ExpressionSyntax exp, ITypeSymbol expConvertedType, Boolean& isNothingLiteral)
                                           在 ICSharpCode.CodeConverter.CSharp.CommonConversions.<SplitVariableDeclarationsAsync>d__34.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<SplitVariableDeclarationsAsync>d__61.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.MethodBodyExecutableStatementVisitor.<VisitLocalDeclarationStatement>d__31.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.PerScopeStateVisitorDecorator.<AddLocalVariablesAsync>d__6.MoveNext()
                                        --- 引发异常的上一位置中堆栈跟踪的末尾 ---
                                           在 System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()
                                           在 ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.<DefaultVisitInnerAsync>d__3.MoveNext()

                                        Input:
                                                                Dim UpdateFile As New Global.PCL.ModComp.CompFile(ModrinthUpdate(Entry.ModrinthHash), Global.PCL.ModComp.CompType.[Mod])

                                         */
                if (!UpdateFile.Available)
                    continue;
                if (ModBase.ModeDebug)
                    ModBase.Log($"[Mod] 本地文件 {Entry.CompFile.FileName} 在 Modrinth 上的最新版为 {UpdateFile.FileName}");
                if (Entry.CompFile.ReleaseDate >= UpdateFile.ReleaseDate || (Entry.CompFile.Hash ?? "") == (UpdateFile.Hash ?? ""))
                    continue;
                // 设置更新日志与更新文件
                if (Entry.UpdateFile is not null && (UpdateFile.Hash ?? "") == (Entry.UpdateFile.Hash ?? "")) // 合并
                {
                    Entry.ChangelogUrls.Add($"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={McVersion}");
                    UpdateFile.DownloadUrls.AddRange(Entry.UpdateFile.DownloadUrls); // 合并下载源
                    Entry.UpdateFile = UpdateFile; // 优先使用 Modrinth 的文件
                }
                else if (Entry.UpdateFile is null || UpdateFile.ReleaseDate >= Entry.UpdateFile.ReleaseDate) // 替换
                {
                    Entry.ChangelogUrls = new List<string>() { $"https://modrinth.com/mod/{ModrinthUpdate(Entry.ModrinthHash)("project_id")}/changelog?g={McVersion}" };
                    Entry.UpdateFile = UpdateFile;
                }
            }
            ModBase.Log($"[Mod] 从 Modrinth 获取本地 Mod 信息结束");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
            IsFailed = true;
        }
        finally
        {
            EndedThreadCount += 1;
        }
    }, "Mod List Detail Loader Modrinth");
            // 从 CurseForge 获取信息
            // 步骤 1：获取 Hash 与对应的工程 ID
            // 步骤 2：尝试读取工程信息缓存，构建其他 Mod 的对应关系
            // 记录对应的 CompFile
            // 步骤 3：获取工程信息
            // 设置 Entry 中的工程信息
            // 查找或许版本更新的文件列表
            // 由于 latestFilesIndexes 是按时间从新到老排序的，所以只需取第一个；如果需要检查多个 releaseType 下的文件，将 > -1 改为 = 1，但这应当并不会获取到更新的文件
            // 步骤 4：获取更新文件信息
            // 设置更新日志与更新文件
            ModBase.RunInNewThread(() => { try { var CurseForgeHashes = new List<uint>(); foreach (var Entry in Mods) { CurseForgeHashes.Add(Entry.CurseForgeHash); if (Loader.IsAbortedWithThread(MainThread)) return; } JContainer CurseForgeRaw = (JContainer)((JObject)ModBase.GetJson(ModDownload.DlModRequest("https://api.curseforge.com/v1/fingerprints/432", "POST", $"{{\"fingerprints\": [{CurseForgeHashes.Join(",")}]}}", "application/json")))["data"]["exactMatches"]; ModBase.Log($"[Mod] 从 CurseForge 获取到 {CurseForgeRaw.Count} 个本地 Mod 的对应信息"); if (!CurseForgeRaw.Any()) return; var CurseForgeMapping = new Dictionary<int, List<LocalCompFile>>(); foreach (var Project in CurseForgeRaw) { string ProjectId = Project["id"].ToString(); uint Hash = (uint)Project["file"]["fileFingerprint"]; foreach (var Entry in Mods) { if (Entry.CurseForgeHash != Hash) continue; if (ModComp.CompProjectCache.ContainsKey(ProjectId) && Entry.Comp is null) Entry.Comp = ModComp.CompProjectCache[ProjectId]; if (!CurseForgeMapping.ContainsKey(Conversions.ToInteger(ProjectId))) CurseForgeMapping[Conversions.ToInteger(ProjectId)] = new List<LocalCompFile>(); CurseForgeMapping[Conversions.ToInteger(ProjectId)].Add(Entry); var File = new ModComp.CompFile((JObject)Project["file"], ModComp.CompType.Mod); if (Entry.CompFile is null || Entry.CompFile.ReleaseDate < File.ReleaseDate) Entry.CompFile = File; } } if (Loader.IsAbortedWithThread(MainThread)) return; ModBase.Log($"[Mod] 需要从 CurseForge 获取 {CurseForgeMapping.Count} 个本地 Mod 的工程信息"); if (!CurseForgeMapping.Any()) return; var CurseForgeProject = ((JObject)ModBase.GetJson(ModDownload.DlModRequest("https://api.curseforge.com/v1/mods", "POST", $"{{\"modIds\": [{CurseForgeMapping.Keys.Join(",")}]}}", "application/json")))["data"]; var UpdateFileIds = new Dictionary<int, List<LocalCompFile>>(); var FileIdToProjectSlug = new Dictionary<int, string>(); foreach (var ProjectJson in CurseForgeProject) { if (ProjectJson["isAvailable"] is not null && !ProjectJson["isAvailable"].ToObject<bool>()) continue; var Project = new ModComp.CompProject((JObject)ProjectJson); foreach (var Entry in CurseForgeMapping[Conversions.ToInteger(Project.Id)]) { if (Entry.Comp is not null && !Entry.Comp.FromCurseForge) { Entry.Comp = Entry.Comp; continue; } Entry.Comp = Project; } if (ModLoaders.Count == 1) { string NewestVersion = null; var NewestFileIds = new List<int>(); foreach (var IndexEntry in ProjectJson["latestFilesIndexes"]) { if (IndexEntry["modLoader"] is null || (int)ModLoaders.Single() != IndexEntry["modLoader"].ToObject<int>()) continue; string IndexVersion = (string)IndexEntry["gameVersion"]; if ((IndexVersion ?? "") != (McVersion ?? "")) continue; if (NewestVersion is not null && ModMinecraft.VersionSortInteger(NewestVersion, IndexVersion) > -1) continue; if ((NewestVersion ?? "") != (IndexVersion ?? "")) { NewestVersion = IndexVersion; NewestFileIds.Clear(); } NewestFileIds.Add(IndexEntry["fileId"].ToObject<int>()); } foreach (var FileId in NewestFileIds) { if (!UpdateFileIds.ContainsKey(FileId)) UpdateFileIds[FileId] = new List<LocalCompFile>(); UpdateFileIds[FileId].AddRange(CurseForgeMapping[Conversions.ToInteger(Project.Id)]); FileIdToProjectSlug[FileId] = Project.Slug; } } } ModBase.Log($"[Mod] 已从 CurseForge 获取本地 Mod 信息，需要获取 {UpdateFileIds.Count} 个用于检查更新的文件信息"); if (!UpdateFileIds.Any()) return; var CurseForgeFiles = ((JObject)ModBase.GetJson(ModDownload.DlModRequest("https://api.curseforge.com/v1/mods/files", "POST", $"{{\"fileIds\": [{UpdateFileIds.Keys.Join(",")}]}}", "application/json")))["data"]; var UpdateFiles = new Dictionary<LocalCompFile, ModComp.CompFile>(); foreach (var FileJson in CurseForgeFiles) { var File = new ModComp.CompFile((JObject)FileJson, ModComp.CompType.Mod); if (!File.Available) continue; foreach (LocalCompFile Entry in UpdateFileIds[Conversions.ToInteger(File.Id)]) { if (UpdateFiles.ContainsKey(Entry) && UpdateFiles[Entry].ReleaseDate >= File.ReleaseDate) continue; UpdateFiles[Entry] = File; } } foreach (var Pair in UpdateFiles) { var Entry = Pair.Key; var UpdateFile = Pair.Value; if (ModBase.ModeDebug) ModBase.Log($"[Mod] 本地文件 {Entry.CompFile.FileName} 在 CurseForge 上的最新版为 {UpdateFile.FileName}"); if (Entry.CompFile.ReleaseDate >= UpdateFile.ReleaseDate || (Entry.CompFile.Hash ?? "") == (UpdateFile.Hash ?? "")) continue; if (Entry.UpdateFile is not null && (UpdateFile.Hash ?? "") == (Entry.UpdateFile.Hash ?? "")) { Entry.ChangelogUrls.Add($"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug[Conversions.ToInteger(UpdateFile.Id)]}/files/{UpdateFile.Id}"); Entry.UpdateFile.DownloadUrls.AddRange(UpdateFile.DownloadUrls); } else if (Entry.UpdateFile is null || UpdateFile.ReleaseDate > Entry.UpdateFile.ReleaseDate) { Entry.ChangelogUrls = new List<string>() { $"https://www.curseforge.com/minecraft/mc-mods/{FileIdToProjectSlug[Conversions.ToInteger(UpdateFile.Id)]}/files/{UpdateFile.Id}" }; Entry.UpdateFile = UpdateFile; } } ModBase.Log($"[Mod] 从 CurseForge 获取 Mod 更新信息结束"); } catch (Exception ex) { ModBase.Log(ex, "从 CurseForge 获取本地 Mod 信息失败"); IsFailed = true; } finally { EndedThreadCount += 1; } }, "Mod List Detail Loader CurseForge"); // 读取已加载的缓存，加快结果出现速度
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // FileId -> 本地 Mod 文件列表
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 倒查防止 CurseForge 返回的内容有漏
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 再次触发修改事件
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // ModLoader 唯一且匹配
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // MC 版本匹配
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 只保留最新 MC 版本
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 合并
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 合并下载源
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 替换
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  // 等待线程结束
            while (EndedThreadCount != 2)
            {
                if (Loader.IsAborted)
                    return;
                Thread.Sleep(10);
            }
            // 保存缓存
            Mods = Mods.Where(m => m.Comp is not null).ToList();
            ModBase.Log($"[Mod] 联网获取本地 Mod 信息完成，为 {Mods.Count} 个 Mod 更新缓存");
            if (!Mods.Any())
                return;
            foreach (var Entry in Mods)
            {
                Entry.CompLoaded = !IsFailed;
                Cache[Entry.ModrinthHash + McVersion + ModLoaders.Join("")] = Entry.ToJson();
            }
            ModBase.WriteFile(ModBase.PathTemp + @"Cache\LocalComp.json", Cache.ToString(ModBase.ModeDebug ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None));
            // 刷新边栏
            ModBase.RunInUi(() => Loader.Input.Frm.RefreshBars());
        }

        public static List<ModComp.CompLoaderType> GetCurrentVersionModLoader()
        {
            var ModLoaders = new List<ModComp.CompLoaderType>();
            if (PageVersionLeft.Version.Version.HasForge)
                ModLoaders.Add(ModComp.CompLoaderType.Forge);
            if (PageVersionLeft.Version.Version.HasNeoForge)
                ModLoaders.Add(ModComp.CompLoaderType.NeoForge);
            if (PageVersionLeft.Version.Version.HasFabric)
                ModLoaders.Add(ModComp.CompLoaderType.Fabric);
            if (PageVersionLeft.Version.Version.HasQuilt)
                ModLoaders.AddRange(new[] { ModComp.CompLoaderType.Fabric, ModComp.CompLoaderType.Quilt });
            if (PageVersionLeft.Version.Version.HasLiteLoader)
                ModLoaders.Add(ModComp.CompLoaderType.LiteLoader);
            if (!ModLoaders.Any())
                ModLoaders.AddRange(new[] { ModComp.CompLoaderType.Forge, ModComp.CompLoaderType.NeoForge, ModComp.CompLoaderType.Fabric, ModComp.CompLoaderType.LiteLoader, ModComp.CompLoaderType.Quilt });
            return ModLoaders;
        }

        public static string GetPathNameByCompType(ModComp.CompType TheType)
        {
            switch (TheType)
            {
                case ModComp.CompType.Mod:
                    {
                        return "mods";
                    }
                case ModComp.CompType.ResourcePack:
                    {
                        return "resourcepacks";
                    }
                case ModComp.CompType.Shader:
                    {
                        return "shaderpacks";
                    }
            }
            return "Nothing";
        }

        /* TODO ERROR: Skipped IfDirectiveTrivia
        #If DEBUG Then
        *//* TODO ERROR: Skipped DisabledTextTrivia
            ''' <summary>
            ''' 检查 Mod 列表中存在的错误，返回错误信息的集合。
            ''' </summary>
            Public Function McModCheck(Version As McVersion, Mods As List(Of McMod)) As List(Of String)
                Dim Result As New List(Of String)
                '令所有 Mod 进行基础检查，并归纳需要检查的 Mod
                Dim CurrentModList As New List(Of McMod)
                For Each ModEntity In Mods
                    If Not ModEntity.IsFileAvailable Then
                        Result.Add("无法读取的 Mod 文件。" & vbCrLf & " - " & ModEntity.Path)
                        Continue For
                    End If
                    If ModEntity.State = McMod.McModState.Fine AndAlso ModEntity.ModId IsNot Nothing Then CurrentModList.Add(ModEntity)
                Next
                '添加默认依赖
                Dim CurrentDependencies As New Dictionary(Of String, String()) '{DependencyVersion, Path}
                If Version.State = McVersionState.Forge Then CurrentDependencies.Add("forge", {Version.Version.ForgeVersion, "Forge"})
                CurrentDependencies.Add("minecraft", {Version.Version.McName, "Minecraft"})
                '检查重复的 Mod，并添加对应的依赖
                For Each ModEntity In CurrentModList
                    For Each PossibleModId In ModEntity.PossibleModId
                        If CurrentDependencies.ContainsKey(PossibleModId) Then
                            If CurrentDependencies(PossibleModId)(2) = 1 Then
                                Result.Add("重复添加了相同的 Mod，请尝试删除其中一个（ModID：" & PossibleModId & "）。" & vbCrLf &
                                    " - " & ModEntity.FileName & vbCrLf &
                                    " - " & CurrentDependencies(PossibleModId)(1))
                            Else
                                Log("[Minecraft] 由于可能有多个 ModID，跳过疑似的重复项（ModID：" & PossibleModId & "）。" & vbCrLf &
                                    " - " & ModEntity.FileName & vbCrLf &
                                    " - " & CurrentDependencies(PossibleModId)(1), LogLevel.Developer)
                            End If
                        Else
                            CurrentDependencies.Add(PossibleModId, {ModEntity.Version, ModEntity.FileName, ModEntity.PossibleModId.Count})
                        End If
                    Next
                Next
                '检查依赖
                For Each ModEntity In CurrentModList
                    Try
                        For Each Dependency In ModEntity.Dependencies
                            Dim ReqId As String = Dependency.Key
                            If ReqId.Count < 2 Then Continue For '确保正常
                            If ReqId = ModEntity.ModId Then Continue For '跳过自体引用
                            If ReqId = "forgemultipartcbe" Then Continue For '跳过莫名其妙的引用
                            If Dependency.Value IsNot Nothing Then
                                '获取分段后的详细版本信息
                                Dim ReqVersion As String = Dependency.Value
                                Dim ReqVersionHeadCanEqual As Boolean = ReqVersion.StartsWithF("[")
                                Dim ReqVersionTailCanEqual As Boolean = ReqVersion.EndsWithF("]")
                                Dim ReqVersionHead As String
                                Dim ReqVersionTail As String
                                If ReqVersion.Contains(",") Then
                                    ReqVersionHead = ReqVersion.Split(",")(0).Trim("([ ".ToCharArray())
                                    ReqVersionTail = ReqVersion.Split(",")(1).Trim("]) ".ToCharArray())
                                Else
                                    ReqVersionHead = ReqVersion.Trim("([]) ".ToCharArray())
                                    ReqVersionTail = ReqVersionHead
                                    If ReqId = "minecraft" AndAlso ReqVersionHead.Split(".").Count = 2 Then
                                        ReqVersionTail = ReqVersionHead.Split(".")(0) & "." & (Val(ReqVersionHead.Split(".")(1)) + 1)
                                        ReqVersionTailCanEqual = False
                                    End If
                                End If
                                If ReqVersionHead.StartsWithF("1.") AndAlso ReqVersionHead.Contains("-") Then ReqVersionHead = ReqVersionHead.Substring(ReqVersionHead.LastIndexOfF("-") + 1)
                                If ReqVersionTail.StartsWithF("1.") AndAlso ReqVersionTail.Contains("-") Then ReqVersionTail = ReqVersionTail.Substring(ReqVersionTail.LastIndexOfF("-") + 1)
                                '获取报错描述文本
                                Dim VersionRequire As String
                                If ReqVersionHead = ReqVersionTail Then
                                    VersionRequire = "应为 " & ReqVersionHead
                                ElseIf ReqVersionHead.Contains(".") AndAlso ReqVersionTail.Contains(".") Then
                                    VersionRequire = "应为 " & ReqVersionHead & " 至 " & ReqVersionTail
                                ElseIf ReqVersionHead.Contains(".") Then
                                    If ReqVersionHeadCanEqual Then
                                        VersionRequire = "最低应为 " & ReqVersionHead
                                    Else
                                        VersionRequire = "应高于 " & ReqVersionHead
                                    End If
                                ElseIf ReqVersionTail.Contains(".") Then
                                    If ReqVersionTailCanEqual Then
                                        VersionRequire = "最高应为 " & ReqVersionHead
                                    Else
                                        VersionRequire = "应低于 " & ReqVersionHead
                                    End If
                                Else
                                    VersionRequire = ""
                                End If
                                '检查前置 Mod 是否存在，并获取其版本
                                If Not CurrentDependencies.ContainsKey(ReqId) Then
                                    Result.Add("缺少前置 Mod：" & ReqId & If(VersionRequire = "", "", "，其版本" & VersionRequire) & "。" & vbCrLf & " - " & ModEntity.FileName)
                                    Continue For
                                End If
                                Dim CurrentVersion As String = If(CurrentDependencies(ReqId)(0), "0.0")
                                If CurrentVersion.StartsWithF("1.") AndAlso CurrentVersion.Contains("-") Then CurrentVersion = CurrentVersion.Substring(CurrentVersion.LastIndexOfF("-") + 1)
                                '对比前置 Mod 头部版本
                                If ReqVersionHead.Contains(".") Then
                                    If VersionSortInteger(ReqVersionHead, CurrentVersion) > If(ReqVersionHeadCanEqual, 0, -1) Then
                                        Result.Add(ReqId.Substring(0, 1).ToUpper & ReqId.Substring(1) & " 版本过低，其版本" & VersionRequire & "，而当前版本为 " & CurrentVersion & "。" & vbCrLf &
                                                   " - " & ModEntity.FileName & If(ReqId <> "minecraft" AndAlso ReqId <> "forge", vbCrLf & " - 前置：" & CurrentDependencies(ReqId)(1), ""))
                                        Continue For
                                    End If
                                End If
                                '对比前置 Mod 尾部版本
                                If ReqVersionTail.Contains(".") Then
                                    If VersionSortInteger(CurrentVersion, ReqVersionTail) > If(ReqVersionTailCanEqual, 0, -1) Then
                                        Result.Add(ReqId.Substring(0, 1).ToUpper & ReqId.Substring(1) & " 版本过高，其版本" & VersionRequire & "，而当前版本为 " & CurrentVersion & "。" & vbCrLf &
                                                   " - " & ModEntity.FileName & If(ReqId <> "minecraft" AndAlso ReqId <> "forge", vbCrLf & " - 前置：" & CurrentDependencies(ReqId)(1), ""))
                                        Continue For
                                    End If
                                End If
                            Else
                                If Not CurrentDependencies.ContainsKey(Dependency.Key) Then
                                    Result.Add("缺少前置 Mod：" & Dependency.Key & "。" & vbCrLf & " - " & ModEntity.FileName)
                                    Continue For
                                End If
                            End If
                        Next
                    Catch ex As Exception
                        Result.Add("检查 Mod 时出错：" & GetExceptionSummary(ex) & vbCrLf & " - " & ModEntity.FileName)
                        Log(ex, "检查 Mod 时出错")
                    End Try
                Next
                If Not Result.Any() Then
                    Log("[Minecraft] Mod 检查未发现异常")
                Else
                    Log("[Minecraft] Mod 检查异常结果：" & vbCrLf & Join(Result, vbCrLf))
                End If
                Return Result
            End Function
        *//* TODO ERROR: Skipped EndIfDirectiveTrivia
        #End If
        */
    }
}