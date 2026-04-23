using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Platform;

/// <summary>
/// Icono en el área de notificación (solo Windows). Requiere hilo STA y bucle de mensajes WinForms.
/// </summary>
internal static class WindowsTrayHost
{
    private static Form? _messageForm;
    private static NotifyIcon? _notifyIcon;

    public static void Start(IHostApplicationLifetime lifetime, IConfiguration configuration, ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!Environment.UserInteractive)
        {
            logger.LogInformation("Entorno no interactivo detectado; se omite bandeja y apertura automática de navegador.");
            return;
        }

        var browserUrl = ResolveTrayBrowserUrl(configuration);
        var autoOpenOnStart = ResolveAutoOpenOnStart(configuration);

        if (autoOpenOnStart)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Pequeña espera para evitar abrir antes de que Kestrel quede listo.
                        await Task.Delay(1200);
                        OpenBrowser(browserUrl);
                    }
                    catch
                    {
                        /* ignorar */
                    }
                });
            });
        }

        var thread = new Thread(() => RunTrayThread(lifetime, browserUrl, logger))
        {
            IsBackground = false,
            Name = "CloudKeepTray"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>URL que abre el navegador al pulsar el icono (Angular en desarrollo suele ser el puerto 4200).</summary>
    private static string ResolveTrayBrowserUrl(IConfiguration configuration)
    {
        var fromConfig = configuration["Tray:OpenBrowserUrl"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        return "http://localhost:4200";
    }

    /// <summary>Controla si el navegador debe abrirse automáticamente al iniciar la app.</summary>
    private static bool ResolveAutoOpenOnStart(IConfiguration configuration)
    {
        var raw = configuration["Tray:AutoOpenBrowserOnStart"];
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        return bool.TryParse(raw, out var parsed) ? parsed : true;
    }

    private static void RunTrayThread(IHostApplicationLifetime lifetime, string browserUrl, ILogger logger)
    {
        ApplicationConfiguration.Initialize();

        lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                if (_messageForm is { IsDisposed: false } f && f.IsHandleCreated)
                {
                    f.BeginInvoke(() =>
                    {
                        try
                        {
                            if (_notifyIcon is not null)
                            {
                                _notifyIcon.Visible = false;
                                _notifyIcon.Dispose();
                                _notifyIcon = null;
                            }

                            Application.Exit();
                        }
                        catch
                        {
                            /* ignorar durante apagado */
                        }
                    });
                }
            }
            catch
            {
                /* ignorar */
            }
        });

        try
        {
            _messageForm = new Form
            {
                ShowInTaskbar = false,
                WindowState = FormWindowState.Minimized,
                Visible = false,
                Size = new Size(0, 0),
                Opacity = 0,
                FormBorderStyle = FormBorderStyle.FixedToolWindow
            };
            _ = _messageForm.Handle;

            _notifyIcon = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Visible = true,
                Text = "Cloud Keep — copias en la nube"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir Cloud Keep", null, (_, _) => OpenBrowser(browserUrl));
            menu.Items.Add("Salir", null, (_, _) => Task.Run(() => lifetime.StopApplication()));
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    OpenBrowser(browserUrl);
            };

            Application.Run(_messageForm);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo mostrar el icono en el área de notificaciones.");
        }
    }

    private static Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "cloudkeep.ico");
        if (File.Exists(path))
            return new Icon(path);

        return SystemIcons.Application;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            /* ignorar */
        }
    }
}
