using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukiDaemon
{
    internal class SystemHelper
    {
        private SystemHelper() { }

        /// <summary>
        /// 设置程序开机启动
        /// </summary>
        /// <param name="strAppPath">应用程序exe所在文件夹</param>
        /// <param name="strAppName">应用程序exe名称</param>
        /// <param name="bIsAutoRun">自动运行状态</param>
        public static void SetAutoRun(string strAppPath, string strAppName, bool bIsAutoRun)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strAppPath)
                    || string.IsNullOrWhiteSpace(strAppName))
                {
                    throw new Exception("应用程序路径或名称为空！");
                }

                RegistryKey reg = Registry.LocalMachine;
                RegistryKey software = reg.OpenSubKey(@"SOFTWARE");
                var runReg = reg.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                if (runReg != null)
                {
                    runReg = reg.CreateSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                }
                //RegistryKey run = reg.CreateSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\");

                if (bIsAutoRun)
                {
                    runReg.SetValue(strAppName, strAppPath);
                }
                else
                {
                    if (null != runReg.GetValue(strAppName))
                    {
                        runReg.DeleteValue(strAppName);
                    }
                }

                runReg.Close();
                reg.Close();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        /// <summary>
        /// 判断是否开机启动
        /// </summary>
        /// <param name="strAppPath">应用程序路径</param>
        /// <param name="strAppName">应用程序名称</param>
        /// <returns></returns>
        public static bool IsAutoRun(string strAppPath, string strAppName)
        {
            try
            {
                RegistryKey reg = Registry.LocalMachine;
                RegistryKey software = reg.OpenSubKey(@"SOFTWARE");
                RegistryKey run = reg.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                object key = run.GetValue(strAppName);
                software.Close();
                run.Close();
                if (null == key || !strAppPath.Equals(key.ToString()))
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
