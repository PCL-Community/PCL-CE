Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.IO

Public Module ModProfile

    ''' <summary>
    ''' 当前选定的档案
    ''' </summary>
    Public SelectedProfile As McProfile = Nothing
    ''' <summary>
    ''' 上次选定的档案编号
    ''' </summary>
    Public LastUsedProfile As Integer = Nothing
    ''' <summary>
    ''' 档案列表
    ''' </summary>
    Public ProfileList As New List(Of McProfile)
    Public IsCreatingProfile As Boolean = False
    ''' <summary>
    ''' 档案操作日志
    ''' </summary>
    Public Sub ProfileLog(content As String, Optional level As LogLevel = LogLevel.Normal)
        Dim output As String = "[Profile] " & content
        Log(output, level)
    End Sub

#Region "类型声明"
    Public Class McProfile
        ''' <summary>
        ''' 档案类型
        ''' </summary>
        Public Type As McLoginType
        Public Uuid As String
        ''' <summary>
        ''' 玩家 ID
        ''' </summary>
        Public Username As String
        ''' <summary>
        ''' 验证服务器地址，用于第三方验证
        ''' </summary>
        Public Server As String
        ''' <summary>
        ''' 验证服务器名称，来自第三方验证服务器返回的 Metadata
        ''' </summary>
        Public ServerName As String
        Public AccessToken As String
        Public RefreshToken As String
        ''' <summary>
        ''' 登录用户名，用于第三方验证
        ''' </summary>
        Public Name As String
        ''' <summary>
        ''' 登录密码，用于第三方验证
        ''' </summary>
        Public Password As String
        ''' <summary>
        ''' 联网验证档案的验证有效期
        ''' </summary>
        Public Expires As Int64
        ''' <summary>
        ''' 档案描述，暂时没做功能
        ''' </summary>
        Public Desc As String
        Public ClientToken As String
        ''' <summary>
        ''' 原始 JSON 数据，用于正版验证部分功能
        ''' </summary>
        Public RawJson As String
        ''' <summary>
        ''' 用于档案列表头像显示的皮肤 ID
        ''' </summary>
        Public SkinHeadId As String
        ''' <summary>
        ''' 用于识别正版档案的 ID 标识符
        ''' </summary>
        <Obsolete("暂时弃用，应当使用 AccessToken 与 RefreshToken")>
        Public IdentityId As String
    End Class
#End Region

#Region "读写档案"
    ''' <summary>
    ''' 重新获取已有档案列表
    ''' </summary>
    Public Sub GetProfile()
        ProfileLog("开始获取本地档案")
        ProfileList.Clear()
        Dim profilePath = Path.Combine(PathAppdataConfig, "profiles.json")
        Try
            If Not Directory.Exists(PathAppdataConfig) Then Directory.CreateDirectory(PathAppdataConfig)
            If Not File.Exists(profilePath) Then
                File.Create(profilePath).Close()
                WriteFile(profilePath, "{""lastUsed"":0,""profiles"":[]}", False) '创建档案列表文件
            End If
            Dim profileJobj As JObject = JObject.Parse(ReadFile(profilePath))
            LastUsedProfile = profileJobj("lastUsed")
            Dim profileListJobj As JArray = profileJobj("profiles")
            For Each Profile In profileListJobj
                Dim newProfile As McProfile = Nothing
                If Profile("type") = "microsoft" Then
                    newProfile = New McProfile With {
                        .Type = McLoginType.Ms,
                        .Uuid = Profile("uuid"),
                        .Username = Profile("username"),
                        .AccessToken = SecretDecrypt(Profile("accessToken")),
                        .RefreshToken = SecretDecrypt(Profile("refreshToken")),
                        .Expires = Profile("expires"),
                        .Desc = Profile("desc"),
                        .RawJson = SecretDecrypt(Profile("rawJson")),
                        .SkinHeadId = Profile("skinHeadId")
                    }
                ElseIf Profile("type") = "authlib" Then
                    newProfile = New McProfile With {
                        .Type = McLoginType.Auth,
                        .Uuid = Profile("uuid"),
                        .Username = Profile("username"),
                        .AccessToken = SecretDecrypt(Profile("accessToken")),
                        .RefreshToken = SecretDecrypt(Profile("refreshToken")),
                        .Expires = Profile("expires"),
                        .Server = Profile("server"),
                        .ServerName = Profile("serverName"),
                        .Name = SecretDecrypt(Profile("name")),
                        .Password = SecretDecrypt(Profile("password")),
                        .ClientToken = SecretDecrypt(Profile("clientToken")),
                        .Desc = Profile("desc"),
                        .SkinHeadId = Profile("skinHeadId")
                    }
                Else
                    newProfile = New McProfile With {
                        .Type = McLoginType.Legacy,
                        .Uuid = Profile("uuid"),
                        .Username = Profile("username"),
                        .Desc = Profile("desc"),
                        .SkinHeadId = Profile("skinHeadId")
                    }
                End If
                ProfileList.Add(newProfile)
            Next
            ProfileLog($"获取到 {ProfileList.Count} 个档案")
        Catch ex As Exception
            Try
                Dim profilePathBak = Path.Combine(PathAppdataConfig, $"profiles.json.bak{DateTime.Now.ToBinary()}")
                File.Move(profilePath, profilePathBak)
            Catch ex1 As Exception
            End Try
            Log(ex, "档案数据读取失败，文件可能意外损坏。已对档案文件进行备份重置。", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 以当前的档案列表写入配置文件
    ''' </summary>
    Public Sub SaveProfile(Optional listJson As JArray = Nothing)
        Try
            Dim json As New JObject
            If listJson IsNot Nothing Then
                json = New JObject From {
                {"lastUsed", LastUsedProfile},
                {"profiles", listJson}
            }
            Else
                Dim list As New JArray
                For Each Profile In ProfileList
                    Dim profileJobj As JObject = Nothing
                    If Profile.Type = McLoginType.Ms Then
                        profileJobj = New JObject From {
                            {"type", "microsoft"},
                            {"uuid", Profile.Uuid},
                            {"username", Profile.Username},
                            {"accessToken", SecretEncrypt(Profile.AccessToken)},
                            {"refreshToken", SecretEncrypt(Profile.RefreshToken)},
                            {"expires", Profile.Expires},
                            {"desc", Profile.Desc},
                            {"rawJson", SecretEncrypt(Profile.RawJson)},
                            {"skinHeadId", Profile.SkinHeadId}
                        }
                    ElseIf Profile.Type = McLoginType.Auth Then
                        profileJobj = New JObject From {
                            {"type", "authlib"},
                            {"uuid", Profile.Uuid},
                            {"username", Profile.Username},
                            {"accessToken", SecretEncrypt(Profile.AccessToken)},
                            {"refreshToken", SecretEncrypt(Profile.RefreshToken)},
                            {"expires", Profile.Expires},
                            {"server", Profile.Server},
                            {"serverName", Profile.ServerName},
                            {"name", SecretEncrypt(Profile.Name)},
                            {"password", SecretEncrypt(Profile.Password)},
                            {"clientToken", SecretEncrypt(Profile.ClientToken)},
                            {"desc", Profile.Desc},
                            {"skinHeadId", Profile.SkinHeadId}
                        }
                    Else
                        profileJobj = New JObject From {
                            {"type", "offline"},
                            {"uuid", Profile.Uuid},
                            {"username", Profile.Username},
                            {"desc", Profile.Desc},
                            {"skinHeadId", Profile.SkinHeadId}
                        }
                    End If
                    list.Add(profileJobj)
                Next
                ProfileLog($"开始保存档案，共 {list.Count} 个")
                json = New JObject From {
                {"lastUsed", LastUsedProfile},
                {"profiles", list}
            }
            End If
            Dim actualFile = Path.Combine(PathAppdataConfig, "profiles.json")
            Dim tempFile = actualFile & ".tmp"
            Dim bakFile = actualFile & ".bak"
            File.WriteAllBytes(tempFile, Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None)))
            If File.Exists(actualFile) Then
                File.Replace(tempFile, actualFile, bakFile)
            Else
                File.Move(tempFile, actualFile)
            End If
            ProfileLog($"档案已保存")
        Catch ex As Exception
            Log(ex, "写入档案列表失败", LogLevel.Feedback)
        End Try
    End Sub
#End Region

#Region "新建与编辑"
    ''' <summary>
    ''' 新建档案
    ''' </summary>
    Public Sub CreateProfile()
        Dim selectedAuthTypeNum As Integer? = Nothing '验证类型序号
        RunInUiWait(Sub()
                        Dim authTypeList As New List(Of IMyRadio) From {
                                New MyListItem With {
                                .Title = "正版验证",
                                .Type = MyListItem.CheckType.RadioBox,
                                .Logo = Logo.IconButtonAuth
                            },
                                New MyListItem With {
                                .Title = "第三方验证",
                                .Type = MyListItem.CheckType.RadioBox,
                                .Logo = Logo.IconButtonThirdparty
                            },
                            New MyListItem With {
                                .Title = "离线验证",
                                .Type = MyListItem.CheckType.RadioBox,
                                .Logo = Logo.IconButtonOffline
                            }
                        }
                        selectedAuthTypeNum = MyMsgBoxSelect(authTypeList, "新建档案 - 选择验证类型", "继续", "取消")
                    End Sub)
        If selectedAuthTypeNum Is Nothing Then Exit Sub
        IsCreatingProfile = True
        If selectedAuthTypeNum = 0 Then '正版验证
            RunInUi(Sub() FrmLaunchLeft.RefreshPage(True, McLoginType.Ms))
        ElseIf selectedAuthTypeNum = 1 Then '第三方验证
            RunInUi(Sub() FrmLaunchLeft.RefreshPage(True, McLoginType.Auth))
        Else '离线验证
            RunInUi(Sub() FrmLaunchLeft.RefreshPage(True, McLoginType.Legacy))
        End If
    End Sub
    ''' <summary>
    ''' 编辑当前档案的 ID
    ''' </summary>
    Public Sub EditProfileId()
        If SelectedProfile.Type = McLoginType.Ms Then
            Dim newUsername As String = Nothing
            RunInUiWait(Sub() newUsername = MyMsgBoxInput("输入新的玩家 ID", "玩家 ID 只能每 30 天更改一次名称，请谨慎考虑！", DefaultInput:=SelectedProfile.Username,
                                                          ValidateRules:=New ObjectModel.Collection(Of Validate) From {New ValidateLength(3, 16), New ValidateRegex("([A-z]|[0-9]|_)+")},
                                                          HintText:="3 - 16 个字符，只可以包含大小写字母、数字、下划线", Button1:="确认", Button2:="取消"))
            If newUsername = Nothing Then Exit Sub
            If String.IsNullOrWhiteSpace(newUsername) Then
                Hint("欲设置的玩家名称为空")
                Exit Sub
            End If
            If MyMsgBox("注意：玩家 ID 只能每 30 天更改一次，请务必谨慎考虑！", "确认修改", "继续修改", "取消", IsWarn:=True) = 2 Then Exit Sub
            RunInNewThread(Sub()
                               Try
                                   Dim checkResult As JObject = GetJson(NetRequestRetry($"https://api.minecraftservices.com/minecraft/profile/name/{newUsername}/available", "GET", Nothing, Nothing, Headers:=New Dictionary(Of String, String) From {{"Authorization", "Bearer " & SelectedProfile.AccessToken}}))
                                   If checkResult("status") = "DUPLICATE" Then
                                       MyMsgBox("此 ID 已被使用，请换一个 ID。", "ID 修改失败", "确认", IsWarn:=True)
                                       Exit Sub
                                   ElseIf checkResult("status") = "NOT_ALLOWED" Then
                                       MyMsgBox("此 ID 包含了除大小写字母、数字、下划线以外的不合法字符。", "ID 修改失败", "确认", IsWarn:=True)
                                       Exit Sub
                                   End If
                                   Dim result As String = NetRequestRetry($"https://api.minecraftservices.com/minecraft/profile/name/{newUsername}", "PUT", "", "application/json", 2, New Dictionary(Of String, String) From {{"Authorization", "Bearer " & SelectedProfile.AccessToken}})
                                   Dim resultJson As JObject = GetJson(result)
                                   Hint($"玩家 ID 修改成功，当前 ID 为：{resultJson("name")}", HintType.Finish)
                                   '更新档案信息
                                   ProfileList.Remove(SelectedProfile)
                                   SelectedProfile.Username = resultJson("name")
                                   ProfileList.Add(SelectedProfile)
                                   LastUsedProfile = ProfileList.Count - 1
                                   '刷新页面信息
                                   FrmLaunchLeft.RefreshPage(True)
                                   SaveProfile()
                               Catch ex As HttpRequestException
                                   Dim exSummary As String = ex.ToString()
                                   If exSummary.Contains("403") Then
                                       MyMsgBox("首次更改 ID 后，必须等待 30 天后才能再次修改 ID，你可以前往官网查询具体时间。", "ID 修改失败", "我知道了")
                                   Else
                                       Log(ex, "修改档案 ID 失败", LogLevel.Msgbox)
                                   End If
                                   Exit Sub
                               End Try
                           End Sub
                    )


        ElseIf SelectedProfile.Type = McLoginType.Auth Then
            Dim server As String = SelectedProfile.Server
            OpenWebsite(server.ToString.Replace("/api/yggdrasil/authserver" + If(server.EndsWithF("/"), "/", ""), "/user/profile"))
        Else
            Dim newUsername As String = Nothing
            RunInUiWait(Sub() newUsername = MyMsgBoxInput("输入新的玩家 ID", DefaultInput:=SelectedProfile.Username,
                                                          ValidateRules:=New ObjectModel.Collection(Of Validate) From {New ValidateLength(3, 16), New ValidateRegex("([A-z]|[0-9]|_)+")},
                                                          HintText:="3 - 16 个字符，只可以包含大小写字母、数字、下划线", Button1:="确认", Button2:="取消"))
            If newUsername = Nothing Then Exit Sub
            EditOfflineUuid(SelectedProfile, GetOfflineUuid(newUsername))
        End If
    End Sub
    ''' <summary>
    ''' 编辑离线档案的 UUID
    ''' </summary>
    ''' <param name="profile">目标档案</param>
    Public Sub EditOfflineUuid(profile As McProfile, Optional uuid As String = Nothing)
        Dim profileIndex = ProfileList.IndexOf(profile)
        Dim newUuid As String
        If uuid IsNot Nothing Then
            newUuid = uuid
            GoTo Write
        End If
        Dim uuidType As Integer
        Dim uuidTypeInput As Integer? = Nothing
        RunInUiWait(Sub()
                        Dim uuidTypeList As New List(Of IMyRadio) From {
                                New MyRadioBox With {.Text = "行业规范 UUID（推荐）"},
                                New MyRadioBox With {.Text = "官方版 PCL UUID（若单人存档的部分信息丢失，可尝试此项）"},
                                New MyRadioBox With {.Text = "自定义"}
                            }
                        uuidTypeInput = MyMsgBoxSelect(uuidTypeList, "新建档案 - 选择 UUID 类型", "继续", "取消")
                    End Sub)
        If uuidTypeInput Is Nothing Then Exit Sub
        uuidType = uuidTypeInput
        If uuidType = 0 Then
            newUuid = GetOfflineUuid(profile.Username, False)
        ElseIf uuidType = 1 Then
            newUuid = GetOfflineUuid(profile.Username, IsLegacy:=True)
        Else
            newUuid = MyMsgBoxInput($"更改档案 {profile.Username} 的 UUID", DefaultInput:=profile.Uuid, HintText:="32 位，不含连字符", ValidateRules:=New ObjectModel.Collection(Of Validate) From {New ValidateLength(32, 32), New ValidateRegex("([A-z]|[0-9]){32}", "UUID 只应该包括英文字母和数字！")}, Button1:="继续", Button2:="取消")
        End If
        If newUuid = Nothing Then Exit Sub
Write:
        ProfileList(profileIndex).Uuid = newUuid
        SelectedProfile = ProfileList(profileIndex)
        SaveProfile()
        Hint("档案信息已保存！", HintType.Finish)
    End Sub
    ''' <summary>
    ''' 编辑指定档案的验证服务器显示名称
    ''' </summary>
    Public Sub EditAuthServerName(profile As McProfile, serverName As String)
        Dim profileIndex = ProfileList.IndexOf(profile)
        ProfileList(profileIndex).ServerName = serverName
        SaveProfile()
        Hint("档案信息已保存！", HintType.Finish)
    End Sub
    ''' <summary>
    ''' 删除特定档案
    ''' </summary>
    ''' <param name="profile">目标档案</param>
    Public Sub RemoveProfile(profile As McProfile)
        ProfileList.Remove(profile)
        LastUsedProfile = Nothing
        SaveProfile()
        Hint("档案删除成功！", HintType.Finish)
    End Sub
#End Region

#Region "导入与导出"
    Public Sub MigrateProfile()
        Dim type As Integer = 3
        If ProfileList.Any() Then
            RunInUiWait(Sub() type = MyMsgBox($"PCL CE 支持导入 HMCL 的全局账户列表，抑或是导出档案列表至 HMCL 全局账户列表。{vbCrLf}你想要...？", "导入 / 导出档案", "导入", "导出", "取消", ForceWait:=True))
            If type = 3 Then Exit Sub
        Else
            RunInUiWait(Sub() type = MyMsgBox($"PCL CE 支持导入 HMCL 的全局账户列表，抑或是导出档案列表至 HMCL 全局账户列表。{vbCrLf}由于目前 PCL CE 不存在任何可用档案，无法导出档案。", "导入 / 导出档案", "导入", "取消", ForceWait:=True))
            If type = 2 Then Exit Sub
        End If
        Dim outsidePath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\.hmcl\accounts.json"
        If type = 1 Then '导入
            Hint("正在导入，请稍后...", HintType.Info)
            RunInNewThread(Sub()
                               Dim importList As JArray
                               Try
                                   importList = JArray.Parse(ReadFile(outsidePath))
                               Catch ex As Exception
                                   importList = New JArray
                               End Try
                               Dim outputList As New List(Of McProfile)
                               Dim importNum As Integer = 0
                               For Each Profile In importList
                                   Dim newProfile As McProfile = Nothing
                                   Dim isDuplicated As Boolean = False
                                   If Profile("type") = "microsoft" Then
                                       For Each ExistProfile In ProfileList
                                           If ExistProfile.Type = McLoginType.Ms AndAlso ExistProfile.Uuid = Profile("uuid") Then '不检查玩家 ID，因为可能改了名字还没刷新
                                               isDuplicated = True
                                               Exit For
                                           End If
                                       Next
                                       newProfile = New McProfile With {
                                                               .Type = McLoginType.Ms,
                                                               .Uuid = Profile("uuid"),
                                                               .Username = Profile("displayName"),
                                                               .AccessToken = "",
                                                               .Expires = 1743779140286,
                                                               .Desc = "",
                                                               .RawJson = "",
                                                               .SkinHeadId = ""
                                                           }
                                       If Not isDuplicated Then outputList.Add(newProfile)
                                   ElseIf Profile("type") = "authlibInjector" Then
                                       newProfile = New McProfile With {
                                                               .Type = McLoginType.Auth,
                                                               .Uuid = Profile("uuid"),
                                                               .Username = Profile("displayName"),
                                                               .AccessToken = "",
                                                               .RefreshToken = "",
                                                               .Expires = 1743779140286,
                                                               .Server = Profile("serverBaseURL"),
                                                               .ServerName = "",
                                                               .Name = Profile("username"),
                                                               .Password = "",
                                                               .ClientToken = Profile("clientToken"),
                                                               .Desc = "",
                                                               .SkinHeadId = ""
                                                           }
                                       Dim response As String = Nothing
                                       Try
                                           response = NetGetCodeByRequestRetry(newProfile.Server.Replace("/authserver", ""), Encoding.UTF8)
                                           Dim serverName As String = JObject.Parse(response)("meta")("serverName").ToString()
                                           newProfile.ServerName = serverName
                                       Catch ex As Exception
                                           ProfileLog("获取服务器名称失败，继续档案添加流程: " & ex.ToString())
                                       End Try
                                       outputList.Add(newProfile)
                                   Else
                                       newProfile = New McProfile With {
                                                               .Type = McLoginType.Legacy,
                                                               .Uuid = Profile("uuid"),
                                                               .Username = Profile("username"),
                                                               .Desc = "",
                                                               .SkinHeadId = ""
                                                           }
                                       outputList.Add(newProfile)
                                   End If
                                   importNum += 1
                               Next
                               For Each Profile In outputList
                                   ProfileList.Add(Profile)
                               Next
                               SaveProfile()
                               Hint($"已导入 {importNum} 个档案，部分档案可能需要重新验证密码！", HintType.Finish)
                               RunInUi(Sub() FrmLoginProfile.RefreshProfileList())
                           End Sub, "Profile Import")
        Else '导出
            Hint("正在导出，请稍后...", HintType.Info)
            Dim existList As JArray
            Try
                existList = JArray.Parse(ReadFile(outsidePath))
            Catch ex As Exception
                existList = New JArray
            End Try
            Dim outputList As New JArray
            Dim outputNum As Integer = 0
            For Each Profile In ProfileList
                Dim newProfile As JObject = Nothing
                If Profile.Type = McLoginType.Ms Then
                    newProfile = New JObject From {
                                           {"uuid", Profile.Uuid},
                                           {"displayName", Profile.Username},
                                           {"tokenType", "Bearer"},
                                           {"accessToken", ""},
                                           {"refreshToken", ""},
                                           {"notAfter", 1743779140286},
                                           {"userid", ""},
                                           {"type", "microsoft"}
                                       }
                ElseIf Profile.Type = McLoginType.Auth Then
                    newProfile = New JObject From {
                                           {"serverBaseURL", Profile.Server},
                                           {"clientToken", ""},
                                           {"displayName", Profile.Username},
                                           {"accessToken", ""},
                                           {"type", "authlibInjector"},
                                           {"uuid", Profile.Uuid},
                                           {"username", Profile.Name}
                                       }
                Else
                    newProfile = New JObject From {
                                           {"uuid", Profile.Uuid},
                                           {"username", Profile.Username},
                                           {"type", "offline"}
                                       }
                End If
                outputList.Add(newProfile)
                outputNum += 1
            Next
            For Each Profile In outputList
                existList.Add(Profile)
            Next
            WriteFile(outsidePath, existList.ToString())
            Hint($"已导出 {outputNum} 个档案，部分档案可能需要重新验证密码！", HintType.Finish)
        End If
    End Sub
#End Region

#Region "离线 UUID 获取"
    ''' <summary>
    ''' 获取离线 UUID
    ''' </summary>
    ''' <param name="userName">玩家 ID</param>
    ''' <param name="isSplited">返回的 UUID 是否有连字符分割</param>
    ''' <param name="isLegacy">是否使用旧版 PCL 生成方式，若为 True 则返回的 UUID 总是不带连字符</param>
    Public Function GetOfflineUuid(userName As String, Optional isSplited As Boolean = False, Optional isLegacy As Boolean = False) As String
        If isLegacy Then
            Dim fullUuid As String = StrFill(userName.Length.ToString("X"), "0", 16) & StrFill(GetHash(userName).ToString("X"), "0", 16)
            Return fullUuid.Substring(0, 12) & "3" & fullUuid.Substring(13, 3) & "9" & fullUuid.Substring(17, 15)
        Else
            Dim md5Hash As MD5 = MD5.Create()
            Dim hash As Byte() = md5Hash.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + userName))
            hash(6) = hash(6) And &HF
            hash(6) = hash(6) Or &H30
            hash(8) = hash(8) And &H3F
            hash(8) = hash(8) Or &H80
            Dim parsed As New Guid(ToUuidString(hash))
            ProfileLog("获取到离线 UUID: " & parsed.ToString())
            If isSplited Then
                Return parsed.ToString()
            Else
                Return parsed.ToString().Replace("-", "")
            End If
        End If
    End Function
    Private Function ToUuidString(bytes As Byte()) As String
        Dim msb As Long = 0
        Dim lsb As Long = 0
        For i As Integer = 0 To 7
            msb = (msb << 8) Or (bytes(i) And &HFF)
        Next
        For i As Integer = 8 To 15
            lsb = (lsb << 8) Or (bytes(i) And &HFF)
        Next
        Return (Digits(msb >> 32, 8) + "-" + Digits(msb >> 16, 4) + "-" + Digits(msb, 4) + "-" + Digits(lsb >> 48, 4) + "-" + Digits(lsb, 12))
    End Function
    Private Function Digits(val As Long, digs As Integer)
        Dim hi As Long = 1L << (digs * 4)
        Return (hi Or (val And (hi - 1))).ToString("X").Substring(1)
    End Function
#End Region

#Region "档案信息获取"
    ''' <summary>
    ''' 获取档案详情信息用于显示
    ''' </summary>
    ''' <param name="profile">目标档案</param>
    ''' <returns>显示的详情信息</returns>
    Public Function GetProfileInfo(profile As McProfile)
        Dim info As String = Nothing
        If profile.Type = McLoginType.Auth Then
            info += "第三方验证"
            If Not String.IsNullOrWhiteSpace(profile.ServerName) Then info += $" / {profile.ServerName}"
        ElseIf profile.Type = McLoginType.Ms Then
            info += "正版验证"
        Else
            info += "离线验证"
        End If
        If Not String.IsNullOrWhiteSpace(profile.Desc) Then info += $"，{profile.Desc}"
        Return info
    End Function
    ''' <summary>
    ''' 获取当前档案的验证信息。
    ''' <param name="targetAuthType">验证类型，若为新档案需填</param>
    ''' </summary>
    Public Function GetLoginData(Optional targetAuthType As McLoginType = Nothing) As McLoginData
        Dim authType As McLoginType = Nothing
        If SelectedProfile Is Nothing Then '新档案
            If Not targetAuthType = Nothing Then
                authType = targetAuthType
            Else
                authType = McLoginType.Legacy
            End If
            If authType = McLoginType.Auth Then
                Return New McLoginServer(McLoginType.Auth) With {
                    .Description = "Authlib-Injector",
                    .Type = McLoginType.Auth,
                    .IsExist = (FrmLoginAuth Is Nothing)
                }
            ElseIf authType = McLoginType.Ms Then
                Return New McLoginMs
            Else
                Return New McLoginLegacy
            End If
        Else '已有档案
            authType = SelectedProfile.Type
            If authType = McLoginType.Auth Then
                Return New McLoginServer(McLoginType.Auth) With {
                    .BaseUrl = SelectedProfile.Server,
                    .UserName = SelectedProfile.Name,
                    .Password = SelectedProfile.Password,
                    .Description = "Authlib-Injector",
                    .Type = McLoginType.Auth,
                    .IsExist = (FrmLoginAuth Is Nothing)
                }
            ElseIf authType = McLoginType.Ms Then
                If McLoginMsLoader.State = LoadState.Finished Then
                    Return New McLoginMs With {
                        .OAuthRefreshToken = SelectedProfile.RefreshToken,
                        .UserName = SelectedProfile.Username,
                        .AccessToken = SelectedProfile.AccessToken,
                        .Uuid = SelectedProfile.Uuid,
                        .ProfileJson = SelectedProfile.RawJson
                    }
                Else
                    Return New McLoginMs With {.OAuthRefreshToken = SelectedProfile.RefreshToken, .UserName = SelectedProfile.Name}
                End If
            Else
                Return New McLoginLegacy With {.UserName = SelectedProfile.Username, .Uuid = SelectedProfile.Uuid}
            End If
        End If
    End Function
    ''' <summary>
    ''' 检查当前档案是否有效
    ''' </summary>
    ''' <returns>若档案验证有效，则返回空字符串，否则返回错误原因</returns>
    Public Function IsProfileValid()
        Select Case SelectedProfile.Type
            Case McLoginType.Legacy
                If SelectedProfile.Username.Trim = "" Then Return "玩家名不能为空！"
                If SelectedProfile.Username.Contains("""") Then Return "玩家名不能包含英文引号！"
                If McInstanceCurrent IsNot Nothing AndAlso
                   ((McInstanceCurrent.Version.McCodeMain = 20 AndAlso McInstanceCurrent.Version.McCodeSub >= 3) OrElse McInstanceCurrent.Version.McCodeMain > 20) AndAlso
                   SelectedProfile.Username.Trim.Length > 16 Then
                    Return "自 1.20.3 起，玩家名至多只能包含 16 个字符！"
                End If
                Return ""
            Case McLoginType.Ms
                Return ""
            Case McLoginType.Auth
                Return ""
        End Select
        Return "未知的验证方式"
    End Function
#End Region

#Region "皮肤"
    Private _isMsSkinChanging As Boolean = False
    Public Sub ChangeSkinMs()
        '检查条件，获取新皮肤
        If _isMsSkinChanging Then
            Hint("正在更改皮肤中，请稍候！")
            Exit Sub
        End If
        If McLoginLoader.State = LoadState.Failed Then
            Hint("登录失败，无法更改皮肤！", HintType.Critical)
            Exit Sub
        End If
        Dim skinInfo As McSkinInfo = McSkinSelect()
        If Not skinInfo.IsVaild Then Exit Sub
        Hint("正在更改皮肤……")
        _isMsSkinChanging = True
        '开始实际获取
        RunInNewThread(
        Sub()
            Try
Retry:
                If McLoginMsLoader.State = LoadState.Loading Then McLoginMsLoader.WaitForExit() '等待登录结束
                '获取登录信息
                If McLoginMsLoader.State <> LoadState.Finished Then McLoginMsLoader.WaitForExit(GetLoginData())
                If McLoginMsLoader.State <> LoadState.Finished Then
                    Hint("登录失败，无法更改皮肤！", HintType.Critical)
                    Return
                End If
                Dim accessToken As String = SelectedProfile.AccessToken

                Dim headers As New Dictionary(Of String, String)
                headers.Add("Authorization", $"Bearer {accessToken}")
                headers.Add("Accept", "*/*")
                headers.Add("User-Agent", "MojangSharp/0.1")
                Dim contents As New MultipartFormDataContent From {
                    {New StringContent(If(skinInfo.IsSlim, "slim", "classic")), "variant"},
                    {New ByteArrayContent(ReadFileBytes(skinInfo.LocalFile)), "file", GetFileNameFromPath(skinInfo.LocalFile)}
                }
                Dim res = NetRequestRetry("https://api.minecraftservices.com/minecraft/profile/skins", "POST", contents, Nothing, Headers:=headers)
                If res.Contains("request requires user authentication") Then
                    Hint("正在登录，将在登录完成后继续更改皮肤……")
                    McLoginMsLoader.Start(GetLoginData(), IsForceRestart:=True)
                    GoTo Retry
                ElseIf res.Contains("""error""") Then
                    Hint("更改皮肤失败：" & GetJson(res)("error"), HintType.Critical)
                    Exit Sub
                End If
                '获取新皮肤地址
                Log("[Skin] 皮肤修改返回值：" & vbCrLf & res)
                Dim resultJson As JObject = GetJson(res)
                If resultJson.ContainsKey("errorMessage") Then Throw New Exception(resultJson("errorMessage").ToString) '#5309
                For Each skin As JObject In resultJson("skins")
                    If skin("state").ToString = "ACTIVE" Then
                        MySkin.ReloadCache(skin("url"))
                        Exit Sub
                    End If
                Next
                Throw New Exception("未知错误（" & res & "）")
            Catch ex As Exception
                If ex.GetType.Equals(GetType(Tasks.TaskCanceledException)) Then
                    Hint("更改皮肤失败：与 Mojang 皮肤服务器的连接超时，请检查你的网络是否通畅！", HintType.Critical)
                Else
                    Log(ex, "更改皮肤失败", LogLevel.Hint)
                End If
            Finally
                _isMsSkinChanging = False
            End Try
        End Sub, "Ms Skin Upload")
    End Sub
#End Region

#Region "旧版迁移"
    ''' <summary>
    ''' 从旧版配置文件迁移档案，不能在 UI 线程调用
    ''' </summary>
    Public Sub MigrateOldProfile()
        ProfileLog("开始从旧版配置迁移档案")
        Dim profileCount As Integer = 0
        '正版档案
        If Not Setup.Get("LoginMsJson") = "{}" Then
            Dim oldMsJson As JObject = GetJson(Setup.Get("LoginMsJson"))
            ProfileLog($"找到 {oldMsJson.Count} 个旧版正版档案信息")
            For Each Profile In oldMsJson
                Dim newProfile As New McProfile With {.Username = Profile.Key, .Uuid = McLoginMojangUuid(Profile.Key, False), .Type = McLoginType.Ms}
                ProfileList.Add(newProfile)
                profileCount += 1
            Next
            SaveProfile()
            ProfileLog("旧版正版档案迁移完成")
            Setup.Reset("LoginMsJson")
        Else
            ProfileLog("无旧版正版档案信息")
        End If
        '离线档案
        If Not String.IsNullOrWhiteSpace(Setup.Get("LoginLegacyName")) Then
            Dim oldOfflineInfo As String() = Setup.Get("LoginLegacyName").Split("¨")
            ProfileLog($"找到 {oldOfflineInfo.Count} 个旧版离线档案信息")
            For Each OfflineId In oldOfflineInfo
                Dim newProfile As New McProfile With {.Username = OfflineId, .Uuid = GetOfflineUuid(OfflineId, IsLegacy:=True), .Type = McLoginType.Legacy} '迁移的档案默认使用旧版 UUID 生成方式以避免存档丢失
                ProfileList.Add(newProfile)
                profileCount += 1
            Next
            SaveProfile()
            ProfileLog("旧版离线档案迁移完成")
            Setup.Reset("LoginLegacyName")
        Else
            ProfileLog("无旧版离线档案信息")
        End If
        '第三方验证档案
        If Not (String.IsNullOrWhiteSpace(Setup.Get("CacheAuthName")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthUuid")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthServerServer")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthUsername")) OrElse String.IsNullOrWhiteSpace(Setup.Get("CacheAuthPass"))) Then
            ProfileLog($"找到旧版第三方验证档案信息")
            Dim newProfile As New McProfile With {.Username = Setup.Get("CacheAuthName"), .Uuid = Setup.Get("CacheAuthUuid"),
                    .Name = Setup.Get("CacheAuthUsername"), .Password = Setup.Get("CacheAuthPass"), .Server = Setup.Get("CacheAuthServerServer") & "/authserver", .Type = McLoginType.Auth}
            ProfileList.Add(newProfile)
            SaveProfile()
            ProfileLog("旧版第三方验证档案迁移完成")
            profileCount += 1
            Setup.Reset("CacheAuthName")
            Setup.Reset("CacheAuthUuid")
            Setup.Reset("CacheAuthServerServer")
            Setup.Reset("CacheAuthUsername")
            Setup.Reset("CacheAuthPass")
        Else
            ProfileLog("无旧版第三方验证档案信息")
        End If
        If Not profileCount = 0 Then Hint($"已自动从旧版配置文件迁移档案，共迁移了 {profileCount} 个档案")
        ProfileLog("档案迁移结束")
    End Sub
#End Region

#Region "获取正版档案 UUID"
    ''' <summary>
    ''' 根据用户名返回对应 UUID，需要多线程
    ''' </summary>
    ''' <param name="name">玩家 ID</param>
    Public Function McLoginMojangUuid(name As String, throwOnNotFound As Boolean)
        If name.Trim.Length = 0 Then Return StrFill("", "0", 32)
        '从缓存获取
        Dim uuid As String = ReadIni(PathTemp & "Cache\Uuid\Mojang.ini", name, "")
        If Len(uuid) = 32 Then Return uuid
        '从官网获取
        Try
            Dim gotJson As JObject = Nothing
            Dim finished = False
            RunInNewThread(Sub()
                               Try
                                   gotJson = NetGetCodeByRequestRetry("https://api.mojang.com/users/profiles/minecraft/" & name, IsJson:=True)
                               Catch ex As Exception
                               Finally
                                   finished = True
                               End Try
                           End Sub, $"{name} Uuid Get")
            While Not finished
                Thread.Sleep(50)
            End While
            If gotJson Is Nothing Then Throw New FileNotFoundException("正版玩家档案不存在（" & name & "）")
            uuid = If(gotJson("id"), "")
        Catch ex As Exception
            Log(ex, "从官网获取正版 UUID 失败（" & name & "）")
            If Not throwOnNotFound AndAlso ex.GetType.Name = "FileNotFoundException" Then
                uuid = GetOfflineUuid(name, IsLegacy:=True) '玩家档案不存在
            Else
                Throw New Exception("从官网获取正版 UUID 失败", ex)
            End If
        End Try
        '写入缓存
        If Not Len(uuid) = 32 Then Throw New Exception("获取的正版 UUID 长度不足（" & uuid & "）")
        WriteIni(PathTemp & "Cache\Uuid\Mojang.ini", name, uuid)
        Return uuid
    End Function
#End Region

End Module
