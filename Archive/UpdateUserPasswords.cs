// =============================================
// 批量更新用户密码为默认密码123456
// 使用方法：在应用程序中调用此方法
// =============================================

using System;
using System.Linq;

/// <summary>
/// 批量更新Sys_UserLogOn表中所有用户的密码为默认密码123456
/// </summary>
public void UpdateAllUserPasswordsToDefault()
{
    try
    {
        // 查询所有需要更新密码的用户（F_UserPassword为空或需要重置）
        var userLogOns = db.Sys_UserLogOn
            .Where(x => string.IsNullOrEmpty(x.F_UserPassword) || x.F_UserPassword == "")
            .ToList();
        
        int updateCount = 0;
        string defaultPassword = "123456";  // 默认密码
        
        foreach (var userLogOn in userLogOns)
        {
            try
            {
                // 第一步：对原始密码进行MD5加密（32位）
                string step1 = JUWX.Utility.Md5.md5(defaultPassword, 32).ToLower();
                
                // 第二步：使用密钥进行DES加密
                string encrypted = JUWX.Utility.DESEncrypt.Encrypt(step1, userLogOn.F_UserSecretkey).ToLower();
                
                // 第三步：对加密结果再次进行MD5加密（32位）
                string finalPassword = JUWX.Utility.Md5.md5(encrypted, 32).ToLower();
                
                // 更新密码
                userLogOn.F_UserPassword = finalPassword;
                userLogOn.F_LastModifyTime = DateTime.Now;
                userLogOn.F_LastModifyUserId = "system";
                
                updateCount++;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"更新用户 {userLogOn.F_UserId} 密码失败：" + ex.Message);
            }
        }
        
        // 保存更改
        db.Sys_UserLogOn.SaveChanges();
        
        LogHelper.WriteLog($"批量更新密码完成，共更新 {updateCount} 个用户的密码为默认密码123456");
        
        return new { success = true, message = $"成功更新 {updateCount} 个用户的密码", count = updateCount };
    }
    catch (Exception ex)
    {
        LogHelper.WriteLog("批量更新用户密码失败：" + ex.Message);
        throw;
    }
}

/// <summary>
/// 为指定用户更新密码为默认密码123456
/// </summary>
/// <param name="userId">用户ID</param>
public void UpdateUserPasswordToDefault(string userId)
{
    try
    {
        var userLogOn = db.Sys_UserLogOn.FirstOrDefault(x => x.F_UserId == userId);
        
        if (userLogOn == null)
        {
            throw new Exception($"用户 {userId} 的登录信息不存在");
        }
        
        if (string.IsNullOrEmpty(userLogOn.F_UserSecretkey))
        {
            // 如果密钥为空，先生成密钥
            userLogOn.F_UserSecretkey = JUWX.Utility.Md5.md5(JUWX.Utility.Common.CreateNo(), 16).ToLower();
        }
        
        string defaultPassword = "123456";  // 默认密码
        
        // 第一步：对原始密码进行MD5加密（32位）
        string step1 = JUWX.Utility.Md5.md5(defaultPassword, 32).ToLower();
        
        // 第二步：使用密钥进行DES加密
        string encrypted = JUWX.Utility.DESEncrypt.Encrypt(step1, userLogOn.F_UserSecretkey).ToLower();
        
        // 第三步：对加密结果再次进行MD5加密（32位）
        string finalPassword = JUWX.Utility.Md5.md5(encrypted, 32).ToLower();
        
        // 更新密码
        userLogOn.F_UserPassword = finalPassword;
        userLogOn.F_LastModifyTime = DateTime.Now;
        userLogOn.F_LastModifyUserId = "system";
        
        db.Sys_UserLogOn.SaveChanges();
        
        LogHelper.WriteLog($"用户 {userId} 的密码已更新为默认密码123456");
    }
    catch (Exception ex)
    {
        LogHelper.WriteLog($"更新用户 {userId} 密码失败：" + ex.Message);
        throw;
    }
}

/// <summary>
/// 为所有缺少密码的用户批量生成登录信息并设置默认密码123456
/// </summary>
public void GenerateUserLogOnWithDefaultPassword()
{
    try
    {
        // 1. 先执行SQL脚本生成基础记录（如果还没有执行）
        // 或者在这里直接生成
        
        // 查询所有缺少Sys_UserLogOn记录的用户
        var usersWithoutLogOn = db.Sys_User
            .Where(x => x.F_DeleteMark == false && x.F_EnabledMark == true)
            .Where(x => !db.Sys_UserLogOn.Any(ul => ul.F_UserId == x.ID))
            .ToList();
        
        int createCount = 0;
        string defaultPassword = "123456";
        
        foreach (var user in usersWithoutLogOn)
        {
            try
            {
                // 生成密钥
                string secretkey = JUWX.Utility.Md5.md5(JUWX.Utility.Common.CreateNo(), 16).ToLower();
                
                // 生成加密密码
                string step1 = JUWX.Utility.Md5.md5(defaultPassword, 32).ToLower();
                string encrypted = JUWX.Utility.DESEncrypt.Encrypt(step1, secretkey).ToLower();
                string finalPassword = JUWX.Utility.Md5.md5(encrypted, 32).ToLower();
                
                // 创建登录信息
                var userLogOn = new Sys_UserLogOn
                {
                    ID = user.ID,
                    F_UserId = user.ID,
                    F_UserSecretkey = secretkey,
                    F_UserPassword = finalPassword,
                    F_LogOnCount = 0,
                    F_UserOnLine = false,
                    F_CheckIPAddress = false,
                    F_LastModifyTime = DateTime.Now,
                    F_LastModifyUserId = "system"
                };
                
                db.Sys_UserLogOn.Add(userLogOn);
                createCount++;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog($"为用户 {user.ID} ({user.F_Account}) 创建登录信息失败：" + ex.Message);
            }
        }
        
        // 保存更改
        db.Sys_UserLogOn.SaveChanges();
        
        LogHelper.WriteLog($"批量创建登录信息完成，共创建 {createCount} 个用户的登录信息，默认密码为123456");
        
        // 2. 更新所有已有记录但密码为空的用户
        UpdateAllUserPasswordsToDefault();
    }
    catch (Exception ex)
    {
        LogHelper.WriteLog("批量生成用户登录信息失败：" + ex.Message);
        throw;
    }
}
