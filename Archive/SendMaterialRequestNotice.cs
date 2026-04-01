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

            // 构建SQL查询，替换日期参数
            string sqlQuery = perm.SQLQuery;
            // 如果SQL中包含占位符，进行替换
            sqlQuery = sqlQuery.Replace("{yymmdd}", yymmdd);
            
            // 使用正则表达式替换SQL中的日期参数
            // 替换日期格式：yymmdd='20260121' 或 @日期
            sqlQuery = System.Text.RegularExpressions.Regex.Replace(sqlQuery, 
                @"yymmdd\s*=\s*'(\d{8})'", 
                $"yymmdd='{yymmdd}'", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // 替换参数化查询中的 @日期
            sqlQuery = System.Text.RegularExpressions.Regex.Replace(sqlQuery, 
                @"@日期", 
                $"'{yymmdd}'", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 执行SQL查询，直接返回DataTable
            var dt = new DataTable();
            try
            {
                // 执行SQL查询（已替换参数）
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

            // 按班组分组统计出勤情况（使用SQL查询返回的"员工状态"字段）
            // 使用字典存储每个班组的统计数据
            var groupStats = new Dictionary<string, Dictionary<string, int>>();
            
            // 初始化总体统计
            int totalCount = dt.Rows.Count;
            int totalNormalCount = 0;      // 正常
            int totalLateCount = 0;        // 迟到
            int totalAbsentCount = 0;      // 旷工
            int totalLeaveCount = 0;       // 请假
            int totalUnknownCount = 0;     // 未知

            foreach (System.Data.DataRow row in dt.Rows)
            {
                // 获取班组（优先使用SQL返回的"班组"字段，如果没有则使用"班组"列名）
                string groupName = row["班组"]?.ToString() ?? row["gdname"]?.ToString() ?? "未知班组";
                // 获取员工状态（SQL查询已计算好）
                string employeeStatus = row["员工状态"]?.ToString() ?? "未知";
                
                // 如果班组不存在，初始化统计
                if (!groupStats.ContainsKey(groupName))
                {
                    groupStats[groupName] = new Dictionary<string, int>
                    {
                        { "total", 0 },
                        { "normal", 0 },
                        { "late", 0 },
                        { "absent", 0 },
                        { "leave", 0 },
                        { "unknown", 0 }
                    };
                }
                
                groupStats[groupName]["total"]++;
                
                // 根据员工状态进行统计
                if (employeeStatus.StartsWith("正常"))
                {
                    groupStats[groupName]["normal"]++;
                    totalNormalCount++;
                }
                else if (employeeStatus.StartsWith("迟到"))
                {
                    groupStats[groupName]["late"]++;
                    totalLateCount++;
                }
                else if (employeeStatus.StartsWith("旷工"))
                {
                    groupStats[groupName]["absent"]++;
                    totalAbsentCount++;
                }
                else if (employeeStatus.StartsWith("请假"))
                {
                    groupStats[groupName]["leave"]++;
                    totalLeaveCount++;
                }
                else
                {
                    groupStats[groupName]["unknown"]++;
                    totalUnknownCount++;
                }
            }

            // 构建文本消息内容（按班组分组显示统计信息）
            var textContent = new System.Text.StringBuilder();
            textContent.AppendLine("您好！");
            textContent.AppendLine($"【员工日出勤通知】");
            textContent.AppendLine($"日期：{yymmdd}");
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            
            // 按班组显示统计信息
            foreach (var group in groupStats.OrderBy(x => x.Key))
            {
                string groupName = group.Key;
                var stats = group.Value;
                int groupTotal = stats["total"];
                int groupNormal = stats["normal"];
                int groupLate = stats["late"];
                int groupAbsent = stats["absent"];
                int groupLeave = stats["leave"];
                int groupUnknown = stats["unknown"];
                int groupPresent = groupNormal + groupLate;  // 实到人数 = 正常 + 迟到
                double attendanceRate = groupTotal > 0 ? (groupPresent * 100.0 / groupTotal) : 0;
                
                textContent.AppendLine($"【{groupName}】");
                textContent.AppendLine($"应到：{groupTotal}人 | 实到：{groupPresent}人（正常：{groupNormal}人，迟到：{groupLate}人）");
                // 如果未知状态为0，则不显示
                if (groupUnknown > 0)
                {
                    textContent.AppendLine($"缺勤：{groupAbsent}人 | 请假：{groupLeave}人 | 未知：{groupUnknown}人 | 出勤率：{attendanceRate:F1}%");
                }
                else
                {
                    textContent.AppendLine($"缺勤：{groupAbsent}人 | 请假：{groupLeave}人 | 出勤率：{attendanceRate:F1}%");
                }
                textContent.AppendLine("");
            }
            
            // 总体统计
            int totalPresentCount = totalNormalCount + totalLateCount;  // 实到人数 = 正常 + 迟到
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            textContent.AppendLine($"【总计】");
            textContent.AppendLine($"应到人数：{totalCount}人");
            textContent.AppendLine($"实到人数：{totalPresentCount}人（正常：{totalNormalCount}人，迟到：{totalLateCount}人）");
            textContent.AppendLine($"缺勤人数：{totalAbsentCount}人");
            textContent.AppendLine($"请假人数：{totalLeaveCount}人");
            // 如果未知状态为0，则不显示
            if (totalUnknownCount > 0)
            {
                textContent.AppendLine($"未知状态：{totalUnknownCount}人");
            }
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
            
            LogHelper.WriteLog($"发送文本消息（权限{perm.PermissionLevel}，用户数{userIds.Count}，班组数{groupStats.Count}，应到{totalCount}人，实到{totalPresentCount}人（正常{totalNormalCount}人，迟到{totalLateCount}人），缺勤{totalAbsentCount}人，请假{totalLeaveCount}人）");

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
                groupCount = groupStats.Count,
                totalCount = totalCount,
                presentCount = totalPresentCount,
                normalCount = totalNormalCount,
                lateCount = totalLateCount,
                absentCount = totalAbsentCount,
                leaveCount = totalLeaveCount,
                unknownCount = totalUnknownCount,
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
