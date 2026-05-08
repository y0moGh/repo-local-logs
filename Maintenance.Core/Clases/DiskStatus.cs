using Maintenance.Core.Entities;
using Maintenance.Core.Externos;
using Maintenance.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Evweb.Code.Procesos.Mantenimiento
{
    public class DiskStatus
    {
        private string AdminMails { get; set; }
        private string Server { get; set; }
        private string fromMail { get; set; }

        public DiskStatus()
        {
            AdminMails = ConfigurationManager.AppSettings["AdminMails"].ToString();
            Server = Environment.MachineName;
            fromMail = ConfigurationManager.AppSettings["fromMail"].ToString();
        }

        public void Start()
        {
            try
            {
                Log.Information("Inicia el proceso de estado de disco");
                ScanFreeSpace();
                Log.Information("Proceso de estado de disco finalizado");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ha ocurrido un error al intentar procesar la solicitud.");
            }
        }

        private void ScanFreeSpace()
        {
            List<Disk> diskList = new List<Disk>();
            string[] disks = ConfigurationManager.AppSettings["DiskToMonitor"].ToString().Split(',');
            double.TryParse(ConfigurationManager.AppSettings["FreeSpacePercentage"].ToString(), out double freeSpacePercentage);

            foreach (string disk in disks)
            {
                try
                {
                    DriveInfo drive = new DriveInfo(disk);

                    if (!drive.IsReady)
                    {
                        diskList.Add(new Disk("DESMONTADO", disk, 0, 1));
                        checkDiskIntegrity(drive, disk);
                        continue;
                    }

                    diskList.Add(new Disk(drive.VolumeLabel, drive.Name, drive.AvailableFreeSpace, drive.TotalSize));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ha ocurrido un error al intentar procesar la solicitud.");
                }

            }

            string drivesWithoutSpace = diskList?.Where(x => x.FreeSpacePercentage <= freeSpacePercentage)?
                .Select(x => $@"
                        <tr>
                            <th scope='row'>{x.Letter}</td>
                            <td>{x.Name}</td>
                            <td>{x.FreeSpace}GB ({x.FreeSpacePercentage}%)</td>
                            <td>{x.TotalSize}GB</td>
                        </tr>")?
                .DefaultIfEmpty(string.Empty)?
                .Aggregate((x, y) => $"{x} {y}");

            if (string.IsNullOrEmpty(drivesWithoutSpace)) return;

            string html = $@"
                    <html>
                        <head>
                            <style>
                                table {{
                                    font-family: arial, sans-serif;
                                    border-collapse: collapse;
                                    width: 100%;
                                }}
                                td, th {{
                                    border: 1px solid #dddddd;
                                    text-align: left;
                                    padding: 8px;
                                }}
                                tr:nth-child(even) {{
                                    background-color: #dddddd;
                                }}
                            </style>
                        </head>
                        <body>
                            <h2>Alerta de espacio en disco</h2>
                            <p>El servidor {Server} tiene los siguientes discos con menos del {freeSpacePercentage}% de espacio libre:</p>
                            <table>
                                <thead>
                                    <tr>
                                        <th scope='col'>Letra</th>
                                        <th scope='col'>Disco</th>
                                        <th scope='col'>Espacio libre (%)</th>
                                        <th scope='col'>Espacio total</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {drivesWithoutSpace}
                                </tbody>
                            </table>
                        </body>
                    </html>
                ";


            new EvwebAPI().SendEmail(new MailModel()
            {
                to = AdminMails,
                fromName = "Stark Solution | Sistema de alertas",
                from = fromMail,
                subject = $"{Server} | Alerta de espacio en disco",
                content = html,
                isHtml = 1,
                now = true,
                attachment = "",
                withReintento = false
            });
        }

        private void checkDiskIntegrity(DriveInfo drive, string disk)
        {
            // Check folder integrities for file named variable <disk>.txt
            string _rootFolderPath = AppDomain.CurrentDomain.BaseDirectory;
            if (!File.Exists($"{_rootFolderPath}/integrity/{disk}.txt"))
            {
                Log.Error($"El disco {disk} no existe y no se encontró el archivo de integridad.");
                return;
            }

            string script = File.ReadAllText($"{_rootFolderPath}/integrity/{disk}.txt");

            Process integrityScript = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                integrityScript.Start();

                string output = integrityScript.StandardOutput.ReadToEnd();
                string error = integrityScript.StandardError.ReadToEnd();

                integrityScript.WaitForExit();

                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Ha ocurrido un error al intentar ejecutar el script de integridad del disco {disk}. Error: {ex}");
            }

        }
    }
}
