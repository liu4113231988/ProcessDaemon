using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ServiceMonitor
{
    public class ServiceMonitorJob : IJob
    {
        ILogger<ServiceMonitorJob> _logger;
        List<ServiceModel> _serviceModels;

        public ServiceMonitorJob(ILogger<ServiceMonitorJob> logger, IOptions<List<ServiceModel>> options)
        {
            _logger = logger;
            _serviceModels = options.Value;
        }


        public Task Execute(IJobExecutionContext context)
        {
            if (_serviceModels != null && _serviceModels.Count > 0)
            {
                foreach (var item in _serviceModels)
                {
                    try
                    {
                        ServiceStartType startType = GetServiceStartType(item.ServiceName);
                        if (startType == ServiceStartType.Disabled)
                        {
                            _logger.LogInformation("服务处于禁用状态，正在修改为自动启动...");
                            SetServiceStartType(item.ServiceName, ServiceStartType.Automatic);
                            _logger.LogInformation("启动类型已修改为自动");
                        }

                        ServiceController sc = new ServiceController(item.ServiceName);
                        if (sc != null && sc.Status == ServiceControllerStatus.Stopped)
                        {
                            _logger.LogInformation("服务处于停止状态，正在启动...");
                            int timeout = 5000; // 5秒
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(timeout));
                            _logger.LogInformation("服务启动成功！");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("服务启动失败" + ex.Message);
                        _logger.LogError(ex.StackTrace);
                    }

                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取服务的启动类型
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <returns>服务启动类型</returns>
        private static ServiceStartType GetServiceStartType(string serviceName)
        {
            // 完整的Windows服务注册表路径（显式声明HKEY_LOCAL_MACHINE）
            string registryPath = $"HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\{serviceName}";

            // 使用Registry.GetValue直接读取（自动关联HKEY_LOCAL_MACHINE）
            object startValueObj = Registry.GetValue(registryPath, "Start", 4);
            if (startValueObj == null)
            {
                throw new ArgumentException($"找不到服务 {serviceName} 的注册表项或Start值");
            }

            int startValue = Convert.ToInt32(startValueObj);

            // Start值的含义：
            // 2 = 自动 | 3 = 手动 | 4 = 禁用
            return startValue switch
            {
                2 => ServiceStartType.Automatic,
                3 => ServiceStartType.Manual,
                4 => ServiceStartType.Disabled,
                _ => ServiceStartType.Disabled
            };
        }

        /// <summary>
        /// 设置服务的启动类型
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="startType">目标启动类型</param>
        private static void SetServiceStartType(string serviceName, ServiceStartType startType)
        {
            string registryPath = $"HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\{serviceName}";

            using (RegistryKey hklmKey = Registry.LocalMachine.OpenSubKey(registryPath, true))
            {
                if (hklmKey == null)
                {
                    throw new ArgumentException($"无法打开服务 {serviceName} 的注册表项（权限不足或服务不存在）");
                }

                // 转换启动类型为注册表值
                int startValue = startType switch
                {
                    ServiceStartType.Automatic => 2,
                    ServiceStartType.Manual => 3,
                    ServiceStartType.Disabled => 4,
                    _ => 4
                };

                hklmKey.SetValue("Start", startValue, RegistryValueKind.DWord);
            }
        }
    }

    /// <summary>
    /// 服务启动类型枚举
    /// </summary>
    public enum ServiceStartType
    {
        Automatic,  // 自动
        Manual,     // 手动
        Disabled    // 禁用
    }
}
