// SQL查询结果类
public class MaterialRequestResult
{
    public string SrcDocNo { get; set; }
    public string DocNo { get; set; }
    public string MATNR { get; set; }
    public string itemname { get; set; }
    public string ItemSpecs { get; set; }
    public decimal? MENGE { get; set; }
    public DateTime? MaterialRequisitionDate { get; set; }
    public string MaterialPreparationStatus { get; set; }
    public string StorgeUser { get; set; }
    public string Remark { get; set; }
}

// 员工日出勤查询结果类
public class EmployeeDayAttendanceResult
{
    public string empcode { get; set; }      // 员工工号
    public string empname { get; set; }      // 员工姓名
    public string gdname { get; set; }       // 组别名称
    public string go1 { get; set; }          // 出勤状态
}

[AcceptVerbs("get", "Post")]
[Route("api/Task/SendMaterialRequestNotice")]
public async Task<IHttpActionResult> SendMaterialRequestNotice(dynamic json)
{
    try
    {
        string ID = json.ID;
        var bn = db.Base_NodeNotice.GetList(x => x.ID == ID).FirstOrDefault();

        if (bn == null)
        {
            return BadRequest("未找到对应的通知配置");
        }

        // 获取当前的企业ID和组织ID（根据实际情况调整获取方式）
        string enterpriseID = json.EnterpriseID ?? bn.EnterpriseID ?? null;
        string orgID = json.OrgID ?? bn.OrgID ?? null;

        // 查询权限配置，根据企业ID和组织ID过滤，只选择权限1-100（领料单权限）
        var permissions = db.MaterialRequest_Permission.GetList(x => x.IsActive == true
            && x.UserIDs != null && x.UserIDs.Trim() != "" && x.SQLQuery != null && x.SQLQuery.Trim() != ""
            && x.PermissionLevel >= 1 && x.PermissionLevel <= 100
            && (x.EnterpriseID == null || x.EnterpriseID == enterpriseID)
            && (x.OrgID == null || x.OrgID == orgID))
            .Select(x => new { 
                PermissionLevel = x.PermissionLevel, 
                UserIDs = x.UserIDs, 
                SQLQuery = x.SQLQuery 
            }).ToList();
        
        if (permissions.Count == 0) return BadRequest("未配置领料单权限信息（请配置权限范围：1-100）");

        var sendResults = new List<object>();
        int totalSentCount = 0;

        foreach (var perm in permissions)
        {
            var userIds = perm.UserIDs.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (userIds.Count == 0) continue;

            // 执行SQL查询，直接返回DataTable
            var dt = new DataTable();
            try
            {
                dt = db.MaterialRequest_Permission.SqlQuery(perm.SQLQuery, null);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"执行权限{perm.PermissionLevel}的SQL查询失败：" + ex.Message);
                sendResults.Add(new { level = perm.PermissionLevel, userCount = userIds.Count, userIds = perm.UserIDs, dataCount = 0, error = ex.Message });
                continue;
            }

            if (dt == null || dt.Rows.Count == 0)
            {
                sendResults.Add(new { level = perm.PermissionLevel, userCount = userIds.Count, userIds = perm.UserIDs, dataCount = 0, message = "无数据" });
                continue;
            }

            string fileName = $"领料单未领完料清单_权限{perm.PermissionLevel}_{dt.Rows.Count}条记录_{DateTime.Now:yyyyMMddHHmmss}.xls";
            FileSystemHelper.SaveExcelToTempPath(dt, "D:\\\\111\\\\" + fileName);

            // 上传并推送
            var msg = new QYWechatServices();
            var rest = await msg.UploadMediaAsync(msg.Gettoken(db.Sys_SecretKey.GetSecretKeyByCurrent(SecretKey.EMPEntWeiXin)), "D:\\\\111\\\\" + fileName, "file");
            LogHelper.WriteLog($"上传文件结果（权限{perm.PermissionLevel}，用户数{userIds.Count}）：" + rest.ToJson());

            db.Sys_Message.SendQYWechatMsg(userIds, SecretKey.EMPEntWeiXin, new Sys_QXWechatFileMsg()
            {
                msgtype = "file",
                file = new JUWX.DataController.Sys.imageclass() { media_id = rest.media_id }
            });

            totalSentCount += userIds.Count;
            sendResults.Add(new { 
                level = perm.PermissionLevel, 
                userCount = userIds.Count, 
                userIds = perm.UserIDs, 
                dataCount = dt.Rows.Count, 
                fileName = fileName 
            });
        }

        bn.LastDo = DateTime.Now;
        db.Base_NodeNotice.SaveChanges();

        return Ok(new { 
            success = true, 
            message = "领料单未领完料通知发送成功", 
            totalUserCount = totalSentCount,
            levelCount = permissions.Count,
            details = sendResults
        });
    }
    catch (Exception ex)
    {
        LogHelper.WriteLog("发送领料单未领完料通知失败：" + ex.Message);
        return InternalServerError(ex);
    }
}

[AcceptVerbs("get", "Post")]
[Route("api/Task/SendEmployeeDayAttendanceNotice")]
public async Task<IHttpActionResult> SendEmployeeDayAttendanceNotice(dynamic json)
{
    try
    {
        string ID = json.ID;
        var bn = db.Base_NodeNotice.GetList(x => x.ID == ID).FirstOrDefault();

        if (bn == null)
        {
            return BadRequest("未找到对应的通知配置");
        }

        // 获取日期参数，默认为今天
        string yymmdd = json.yymmdd ?? DateTime.Now.ToString("yyyyMMdd");
        // 获取当前的企业ID和组织ID（根据实际情况调整获取方式）
        string enterpriseID = json.EnterpriseID ?? bn.EnterpriseID ?? null;
        string orgID = json.OrgID ?? bn.OrgID ?? null;

        // 查询权限配置，根据企业ID和组织ID过滤，只选择权限101-200（日出勤权限）
        var permissions = db.MaterialRequest_Permission.GetList(x => x.IsActive == true
            && x.UserIDs != null && x.UserIDs.Trim() != "" && x.SQLQuery != null && x.SQLQuery.Trim() != ""
            && x.PermissionLevel >= 101 && x.PermissionLevel <= 200  // 日出勤权限范围
            && (x.EnterpriseID == null || x.EnterpriseID == enterpriseID)
            && (x.OrgID == null || x.OrgID == orgID))
            .Select(x => new { 
                PermissionLevel = x.PermissionLevel, 
                UserIDs = x.UserIDs, 
                SQLQuery = x.SQLQuery 
            }).ToList();
        
        if (permissions.Count == 0) return BadRequest("未配置日出勤权限信息（请配置权限范围：101-200）");

        var sendResults = new List<object>();
        int totalSentCount = 0;

        foreach (var perm in permissions)
        {
            var userIds = perm.UserIDs.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (userIds.Count == 0) continue;

            // 构建SQL查询，替换日期和部门参数
            string sqlQuery = perm.SQLQuery;
            // 如果SQL中包含占位符，进行替换
            sqlQuery = sqlQuery.Replace("{yymmdd}", yymmdd);
            
            // 使用正则表达式替换SQL中的日期和部门参数
            // 替换日期格式：yymmdd='20260121'
            sqlQuery = System.Text.RegularExpressions.Regex.Replace(sqlQuery, 
                @"yymmdd\s*=\s*'(\d{8})'", 
                $"yymmdd='{yymmdd}'", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 执行SQL查询，直接返回DataTable
            var dt = new DataTable();
            try
            {
                // 如果SQL中包含参数化查询，需要处理参数
                // 这里假设SQL可以直接执行，如果需要参数化，需要根据实际情况调整
                dt = db.MaterialRequest_Permission.SqlQuery(sqlQuery, null);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"执行权限{perm.PermissionLevel}的日出勤SQL查询失败：" + ex.Message);
                sendResults.Add(new { level = perm.PermissionLevel, userCount = userIds.Count, userIds = perm.UserIDs, dataCount = 0, error = ex.Message });
                continue;
            }

            if (dt == null || dt.Rows.Count == 0)
            {
                sendResults.Add(new { level = perm.PermissionLevel, userCount = userIds.Count, userIds = perm.UserIDs, dataCount = 0, message = "无数据" });
                continue;
            }

            // 按部门分组统计出勤情况
            TimeSpan lateThreshold = TimeSpan.Parse("08:00");  // 迟到阈值：08:00
            
            // 使用字典存储每个部门的统计数据
            var deptStats = new Dictionary<string, Dictionary<string, int>>();
            
            // 初始化总体统计
            int totalCount = dt.Rows.Count;
            int totalPresentCount = 0;
            int totalAbsentCount = 0;
            int totalLateCount = 0;

            foreach (System.Data.DataRow row in dt.Rows)
            {
                string gdname = row["gdname"]?.ToString() ?? "未知部门";
                string go1 = row["go1"]?.ToString() ?? "";
                
                // 如果部门不存在，初始化统计
                if (!deptStats.ContainsKey(gdname))
                {
                    deptStats[gdname] = new Dictionary<string, int>
                    {
                        { "total", 0 },
                        { "present", 0 },
                        { "absent", 0 },
                        { "late", 0 }
                    };
                }
                
                deptStats[gdname]["total"]++;
                
                // 判断出勤状态（go1是时间格式，大于08:00为迟到，没有数据为缺勤）
                if (string.IsNullOrWhiteSpace(go1))
                {
                    deptStats[gdname]["absent"]++;
                    totalAbsentCount++;
                }
                else
                {
                    // 尝试解析时间（go1是时间格式，如"08:00"）
                    try
                    {
                        // 处理时间格式，支持 "08:00"、"8:00"、"08:00:00"、"8:0" 等格式
                        string timeStr = go1.Trim();
                        
                        // 如果包含秒，去掉秒部分
                        if (timeStr.Contains(":"))
                        {
                            string[] parts = timeStr.Split(':');
                            if (parts.Length >= 2)
                            {
                                // 格式化为 HH:mm
                                int hours = int.Parse(parts[0]);
                                int minutes = int.Parse(parts[1]);
                                timeStr = $"{hours:D2}:{minutes:D2}";
                            }
                        }
                        
                        TimeSpan checkInTime = TimeSpan.Parse(timeStr);
                        
                        if (checkInTime > lateThreshold)
                        {
                            deptStats[gdname]["late"]++;
                            deptStats[gdname]["present"]++;
                            totalLateCount++;
                            totalPresentCount++;
                        }
                        else
                        {
                            deptStats[gdname]["present"]++;
                            totalPresentCount++;
                        }
                    }
                    catch
                    {
                        // 如果时间解析失败，视为缺勤
                        deptStats[gdname]["absent"]++;
                        totalAbsentCount++;
                    }
                }
            }

            // 构建文本消息内容（按部门分组显示统计信息）
            var textContent = new System.Text.StringBuilder();
            textContent.AppendLine("您好！");
            textContent.AppendLine($"【员工日出勤通知】");
            textContent.AppendLine($"日期：{yymmdd}");
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            
            // 按部门显示统计信息
            foreach (var dept in deptStats.OrderBy(x => x.Key))
            {
                string deptName = dept.Key;
                var stats = dept.Value;
                int deptTotal = stats["total"];
                int deptPresent = stats["present"];
                int deptAbsent = stats["absent"];
                int deptLate = stats["late"];
                double attendanceRate = deptTotal > 0 ? (deptPresent * 100.0 / deptTotal) : 0;
                
                textContent.AppendLine($"【{deptName}】");
                textContent.AppendLine($"应到：{deptTotal}人 | 实到：{deptPresent}人 | 缺勤：{deptAbsent}人 | 迟到：{deptLate}人 | 出勤率：{attendanceRate:F1}%");
                textContent.AppendLine("");
            }
            
            // 总体统计
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            textContent.AppendLine($"【总计】");
            textContent.AppendLine($"应到人数：{totalCount}人");
            textContent.AppendLine($"实到人数：{totalPresentCount}人");
            textContent.AppendLine($"缺勤人数：{totalAbsentCount}人");
            textContent.AppendLine($"迟到人数：{totalLateCount}人");
            textContent.AppendLine($"出勤率：{(totalCount > 0 ? (totalPresentCount * 100.0 / totalCount).ToString("F1") : "0")}%");
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            textContent.AppendLine("");
            textContent.AppendLine("详细数据请查看附件Excel文件。");
            textContent.AppendLine("感谢您的关注！");

            // 发送文本消息
            db.Sys_Message.SendQYWechatMsg(userIds, SecretKey.EMPEntWeiXin, new Sys_QXWechatTextMsg()
            {
                msgtype = "text",
                text = new JUWX.DataController.Sys.textclass() { content = textContent.ToString() }
            });
            
            LogHelper.WriteLog($"发送文本消息（权限{perm.PermissionLevel}，用户数{userIds.Count}，部门数{deptStats.Count}，应到{totalCount}人，实到{totalPresentCount}人，缺勤{totalAbsentCount}人，迟到{totalLateCount}人）");

            // 生成并发送Excel文件
            string fileName = $"员工日出勤清单_{yymmdd}_权限{perm.PermissionLevel}_{dt.Rows.Count}条记录_{DateTime.Now:yyyyMMddHHmmss}.xls";
            FileSystemHelper.SaveExcelToTempPath(dt, "D:\\\\111\\\\" + fileName);

            // 上传并推送Excel文件
            var msg = new QYWechatServices();
            var rest = await msg.UploadMediaAsync(msg.Gettoken(db.Sys_SecretKey.GetSecretKeyByCurrent(SecretKey.EMPEntWeiXin)), "D:\\\\111\\\\" + fileName, "file");
            LogHelper.WriteLog($"上传文件结果（权限{perm.PermissionLevel}，用户数{userIds.Count}）：" + rest.ToJson());

            db.Sys_Message.SendQYWechatMsg(userIds, SecretKey.EMPEntWeiXin, new Sys_QXWechatFileMsg()
            {
                msgtype = "file",
                file = new JUWX.DataController.Sys.imageclass() { media_id = rest.media_id }
            });

            totalSentCount += userIds.Count;
            sendResults.Add(new { 
                level = perm.PermissionLevel, 
                userCount = userIds.Count, 
                userIds = perm.UserIDs, 
                dataCount = dt.Rows.Count,
                deptCount = deptStats.Count,
                totalCount = totalCount,
                presentCount = totalPresentCount,
                absentCount = totalAbsentCount,
                lateCount = totalLateCount,
                fileName = fileName,
                yymmdd = yymmdd
            });
        }

        bn.LastDo = DateTime.Now;
        db.Base_NodeNotice.SaveChanges();

        return Ok(new { 
            success = true, 
            message = $"员工日出勤通知发送成功（日期：{yymmdd}）", 
            totalUserCount = totalSentCount,
            levelCount = permissions.Count,
            yymmdd = yymmdd,
            details = sendResults
        });
    }
    catch (Exception ex)
    {
        LogHelper.WriteLog("发送员工日出勤通知失败：" + ex.Message);
        return InternalServerError(ex);
    }
}
