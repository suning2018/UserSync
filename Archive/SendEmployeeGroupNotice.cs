// 员工班组未维护查询结果类
public class EmployeeGroupResult
{
    public string empcode { get; set; }      // 员工工号
    public string empname { get; set; }      // 员工姓名
    public string ManufactureGroup { get; set; }  // 班组名称（应为空或NULL）
    public string Code { get; set; }          // 员工编码
    public string Name { get; set; }          // 员工姓名
}

/// <summary>
/// 发送员工班组未维护通知
/// 功能：查询班组未维护的员工，通过企业微信发送通知
/// 权限范围：201-300（员工班组未维护权限）
/// </summary>
/// <param name="json">包含ID、EnterpriseID、OrgID等参数</param>
/// <returns>通知发送结果</returns>
[AcceptVerbs("get", "Post")]
[Route("api/Task/SendEmployeeGroupNotice")]
public async Task<IHttpActionResult> SendEmployeeGroupNotice(dynamic json)
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

        // 查询权限配置，根据企业ID和组织ID过滤，只选择权限201-300（员工班组未维护权限）
        var permissions = db.MaterialRequest_Permission.GetList(x => x.IsActive == true
            && x.UserIDs != null && x.UserIDs.Trim() != "" && x.SQLQuery != null && x.SQLQuery.Trim() != ""
            && x.PermissionLevel >= 201 && x.PermissionLevel <= 300  // 员工班组未维护权限范围
            && (x.EnterpriseID == null || x.EnterpriseID == enterpriseID)
            && (x.OrgID == null || x.OrgID == orgID))
            .Select(x => new { 
                PermissionLevel = x.PermissionLevel, 
                UserIDs = x.UserIDs, 
                SQLQuery = x.SQLQuery 
            }).ToList();
        
        if (permissions.Count == 0) return BadRequest("未配置员工班组未维护权限信息（请配置权限范围：201-300）");

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

            // 统计信息（SQL查询已只返回未维护班组的员工）
            int totalCount = dt.Rows.Count;

            // 构建文本消息内容
            var textContent = new System.Text.StringBuilder();
            textContent.AppendLine("您好！");
            textContent.AppendLine($"【员工班组未维护通知】");
            textContent.AppendLine($"统计时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            textContent.AppendLine($"未维护班组员工数：{totalCount}人");
            textContent.AppendLine("━━━━━━━━━━━━━━━━━━━━");
            textContent.AppendLine("");
            textContent.AppendLine("详细数据请查看附件Excel文件。");
            textContent.AppendLine("请及时维护员工班组信息！");
            textContent.AppendLine("感谢您的关注！");

            // 发送文本消息
            db.Sys_Message.SendQYWechatMsg(userIds, SecretKey.EMPEntWeiXin, new Sys_QXWechatTextMsg()
            {
                msgtype = "text",
                text = new JUWX.DataController.Sys.textclass() { content = textContent.ToString() }
            });
            
            LogHelper.WriteLog($"发送文本消息（权限{perm.PermissionLevel}，用户数{userIds.Count}，未维护员工数{totalCount}人）");

            // 生成并发送Excel文件
            string fileName = $"员工班组未维护清单_权限{perm.PermissionLevel}_{dt.Rows.Count}条记录_{DateTime.Now:yyyyMMddHHmmss}.xls";
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
                unmaintainedCount = totalCount,
                fileName = fileName
            });
        }

        bn.LastDo = DateTime.Now;
        db.Base_NodeNotice.SaveChanges();

        return Ok(new { 
            success = true, 
            message = "员工班组未维护通知发送成功", 
            totalUserCount = totalSentCount,
            levelCount = permissions.Count,
            details = sendResults
        });
    }
    catch (Exception ex)
    {
        LogHelper.WriteLog("发送员工班组未维护通知失败：" + ex.Message);
        return InternalServerError(ex);
    }
}
