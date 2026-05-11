using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace CloudKeep.SeleniumTests;

[TestCaseOrderer("CloudKeep.SeleniumTests.PriorityTestCaseOrderer", "CloudKeep.SeleniumTests")]
public sealed class CloudKeepHomePageTests : IDisposable
{
    private static string? CreatedDestinationName;
    private static string? CreatedAwsDestinationName;
    private static string? CreatedJobName;
    private static string? CreatedJobSourceDirectory;
    private static string? CreatedScheduledJobSourceDirectory;

    private readonly IWebDriver? _driver;
    private readonly WebDriverWait? _wait;

    public CloudKeepHomePageTests()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        var options = new ChromeOptions();
        if (SeleniumTestSettings.Headless)
            options.AddArgument("--headless=new");

        options.AddArgument("--start-maximized");
        options.AddArgument("--disable-gpu");

        _driver = new ChromeDriver(options);
        _driver.Manage().Window.Maximize();
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
    }

    [Fact, TestPriority(10)]
    public void LoginPage_ShowsPasswordForm()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(LoginPage_ShowsPasswordForm),
            "Valida que la pantalla de login muestre el formulario y permita mostrar u ocultar la contrasena.",
            _driver!,
            () =>
            {
                GoTo("/login");
                ClearBrowserStorage();
                GoTo("/login");

                Assert.Equal("password", PasswordInput().GetAttribute("type"));

                _driver!.FindElement(By.CssSelector(".password-toggle")).Click();

                Assert.Equal("text", PasswordInput().GetAttribute("type"));
                Assert.Contains("Cloud Keep", _driver.Title, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Ingresar", _driver.PageSource, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact, TestPriority(10)]
    public void Login_WithDefaultPassword_NavigatesToDashboard()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Login_WithDefaultPassword_NavigatesToDashboard),
            "Valida que el usuario pueda iniciar sesion y llegar al dashboard.",
            _driver!,
            () =>
            {
                Login();

                WaitForHeading("Dashboard");
                Assert.Equal(SeleniumTestSettings.BaseUrl.AbsolutePath, _driver!.UrlAsUri().AbsolutePath);
                Assert.True(_driver!.FindElement(By.CssSelector(".sidebar-brand")).Displayed);
            });
    }

    [Fact, TestPriority(10)]
    public void Login_WithWrongPassword_ShowsErrorAndStaysOnLogin()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Login_WithWrongPassword_ShowsErrorAndStaysOnLogin),
            "Valida que una contrasena incorrecta muestre error y no permita acceder al dashboard.",
            _driver!,
            () =>
            {
                GoTo("/login");
                ClearBrowserStorage();
                GoTo("/login");

                PasswordInput().SendKeys("wrong-password");
                _driver!.FindElement(By.CssSelector("button[type='submit']")).Click();

                var alert = _wait!.Until(driver => driver.FindElement(By.CssSelector("[role='alert']")));

                Assert.Contains("Contraseña incorrecta", alert.Text, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("/login", _driver.UrlAsUri().AbsolutePath);
                Assert.Empty(_driver.FindElements(By.CssSelector(".sidebar-brand")));
            });
    }

    [Theory, TestPriority(10)]
    [InlineData("Trabajos", "/trabajos", "Trabajos")]
    [InlineData("Destinos", "/destinos", "Destinos")]
    [InlineData("Scripts", "/scripts", "Scripts pre / post copiado")]
    [InlineData("Reportes", "/reportes", "Reporte de trabajos")]
    [InlineData("Configuración", "/configuracion", "Configuración")]
    public void Sidebar_NavigatesToMainSections(string linkText, string expectedPath, string expectedHeading)
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            $"{nameof(Sidebar_NavigatesToMainSections)}_{linkText}",
            $"Valida que el menu lateral abra la seccion {linkText}.",
            _driver!,
            () =>
            {
                Login();

                FindSidebarLink(linkText).Click();

                WaitForPath(expectedPath);
                WaitForHeading(expectedHeading);
            });
    }

    [Fact, TestPriority(10)]
    public void Dashboard_CtaOpensNewJobWizard()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Dashboard_CtaOpensNewJobWizard),
            "Valida que el boton principal del dashboard abra el asistente de nuevo trabajo.",
            _driver!,
            () =>
            {
                Login();

                _driver!.FindElement(By.LinkText("Agregar nuevo trabajo")).Click();

                WaitForPath("/trabajos/nuevo");
                WaitForHeading("Nuevo trabajo");
            });
    }

    [Fact, TestPriority(3)]
    public void Jobs_CanCreateJobThroughWizard()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Jobs_CanCreateJobThroughWizard),
            "Valida que se pueda agregar un trabajo usando el asistente de nuevo trabajo.",
            _driver!,
            () =>
            {
                Login();

                var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var jobName = $"Selenium Trabajo {suffix}";
                var jobDescription = "Trabajo creado por prueba Selenium desde el wizard.";
                var sourceDirectory = Path.Combine(FindRepositoryRoot(), "TestResults", "Selenium", "JobSources", $"job-source-{suffix}");
                EnsureJobSourceDirectory(sourceDirectory);

                GoTo("/trabajos/nuevo");
                WaitForHeading("Nuevo trabajo");

                var generalInputs = _driver!.FindElements(By.CssSelector("input.form-control"));
                SetInputValue(generalInputs[0], jobName);
                SetInputValue(_driver.FindElement(By.CssSelector("textarea.form-control")), jobDescription);
                ClickWizardNext();

                SelectDestinationForJob();
                ClickWizardNext();

                WaitForHeading("Origen del respaldo");
                SetInputValue(_driver.FindElement(By.CssSelector("input.font-monospace")), sourceDirectory);
                ValidateSourceFolderInWizard();
                ClickWizardNext();

                WaitForHeading("Programación (Schedule)");
                ClickWizardNext();

                WaitForHeading("Scripts pre / post (opcional)");
                ClickWizardNext();

                WaitForPath("/trabajos");
                WaitForToast("Trabajo creado");
                WaitForJobInTable(jobName, jobDescription);
                CreatedJobName = jobName;
                CreatedJobSourceDirectory = sourceDirectory;
            });
    }

    [Fact, TestPriority(4)]
    public void Jobs_ManualExecution_ShowsSuccessfulReport()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Jobs_ManualExecution_ShowsSuccessfulReport),
            "Valida que la ejecucion manual de un trabajo creado quede registrada como exitosa en reportes.",
            _driver!,
            () =>
            {
                Login();

                var jobName = CreatedJobName;
                Assert.False(string.IsNullOrWhiteSpace(jobName), "Primero debe ejecutarse la prueba que crea el trabajo por wizard.");
                Assert.False(string.IsNullOrWhiteSpace(CreatedJobSourceDirectory), "Primero debe ejecutarse la prueba que prepara la carpeta origen del trabajo.");
                EnsureJobSourceDirectory(CreatedJobSourceDirectory!);

                GoTo("/trabajos");
                WaitForHeading("Trabajos");

                ClickRunJob(jobName!);
                WaitForManualExecutionCompletedToast();

                FindSidebarLink("Reportes").Click();
                WaitForPath("/reportes");
                WaitForHeading("Reporte de trabajos");
                FilterExecutionReportByJob(jobName!);
                WaitForManualExecutionReport(jobName!);
            });
    }

    [Fact, TestPriority(5)]
    public void Jobs_AutomaticExecution_ShowsSuccessfulReport()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Jobs_AutomaticExecution_ShowsSuccessfulReport),
            "Valida que la ejecucion automatica de un trabajo programado quede registrada como exitosa en reportes.",
            _driver!,
            () =>
            {
                Login();

                var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var jobName = $"Selenium Automatico {suffix}";
                var jobDescription = "Trabajo automatico creado por prueba Selenium desde el wizard.";
                var sourceDirectory = Path.Combine(FindRepositoryRoot(), "TestResults", "Selenium", "JobSources", $"scheduled-job-source-{suffix}");
                var scheduledUtc = DateTime.UtcNow.AddMinutes(2);
                EnsureJobSourceDirectory(sourceDirectory);

                CreateJobThroughWizard(jobName, jobDescription, sourceDirectory, scheduledUtc);
                CreatedScheduledJobSourceDirectory = sourceDirectory;

                WaitForScheduledExecutionTime(scheduledUtc);
                EnsureJobSourceDirectory(CreatedScheduledJobSourceDirectory!);

                FindSidebarLink("Reportes").Click();
                WaitForPath("/reportes");
                WaitForHeading("Reporte de trabajos");
                FilterExecutionReportByJob(jobName);
                WaitForScheduledExecutionReport(jobName);
            });
    }

    [Fact, TestPriority(6)]
    public void Jobs_SixCloudDestinations_RunTogether_ShowSuccessfulReports()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Jobs_SixCloudDestinations_RunTogether_ShowSuccessfulReports),
            "Valida que 3 destinos Azure y 3 destinos AWS puedan ejecutar trabajos al mismo tiempo y registrarse exitosamente en reportes.",
            _driver!,
            () =>
            {
                Login();

                var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var jobs = new List<CloudDestinationJobInput>();

                for (var index = 1; index <= 3; index++)
                {
                    var destination = new AzureDestinationInput(
                        $"Selenium Azure Multi {index} {suffix}",
                        RequiredEnvironment("CLOUDKEEP_AZURE_CONTAINER_NAME"),
                        RequiredEnvironment("CLOUDKEEP_AZURE_CONNECTION_STRING"),
                        $"selenium-azure-multi-{index}-{suffix}");

                    CreateAzureDestinationAndValidate(destination);
                    jobs.Add(CreateCloudDestinationJobInput(destination.Name, $"Azure {index}", suffix));
                }

                for (var index = 1; index <= 3; index++)
                {
                    var destination = new AwsDestinationInput(
                        $"Selenium AWS Multi {index} {suffix}",
                        RequiredEnvironment("CLOUDKEEP_AWS_BUCKET_NAME"),
                        RequiredEnvironment("CLOUDKEEP_AWS_REGION"),
                        RequiredEnvironment("CLOUDKEEP_AWS_ACCESS_KEY_ID"),
                        RequiredEnvironment("CLOUDKEEP_AWS_SECRET_ACCESS_KEY"),
                        $"selenium/aws/multi-{index}-{suffix}");

                    CreateAwsDestinationAndValidate(destination);
                    jobs.Add(CreateCloudDestinationJobInput(destination.Name, $"AWS {index}", suffix));
                }

                foreach (var job in jobs)
                {
                    EnsureJobSourceDirectory(job.SourceDirectory);
                    CreateJobThroughWizard(job.Name, job.Description, job.SourceDirectory, scheduledUtc: null, job.DestinationName);
                }

                GoTo("/trabajos");
                WaitForHeading("Trabajos");
                foreach (var job in jobs)
                {
                    EnsureJobSourceDirectory(job.SourceDirectory);
                    ClickRunJob(job.Name);
                }

                FindSidebarLink("Reportes").Click();
                WaitForPath("/reportes");
                WaitForHeading("Reporte de trabajos");
                foreach (var job in jobs)
                {
                    FilterExecutionReportByJob(job.Name);
                    WaitForManualExecutionReport(job.Name);
                }
            });
    }

    [Fact, TestPriority(10)]
    public void Settings_CanUpdateScriptExecutionTimeout()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Settings_CanUpdateScriptExecutionTimeout),
            "Valida que Configuracion permita cambiar el tiempo maximo de ejecucion de scripts y restaurar el valor original.",
            _driver!,
            () =>
            {
                Login();
                GoTo("/configuracion");
                WaitForHeading("Configuración");

                var timeoutInput = WaitForElement(By.Id("scriptTimeout"));
                var originalValue = int.Parse(timeoutInput.GetAttribute("value") ?? "2");
                var testValue = originalValue == 3 ? 4 : 3;

                SetInputValue(timeoutInput, testValue.ToString());
                SafeClick(FindScriptTimeoutSaveButton());
                WaitForToast("Tiempo de espera de scripts guardado.");

                GoTo("/configuracion");
                Assert.Equal(testValue.ToString(), WaitForElement(By.Id("scriptTimeout")).GetAttribute("value"));

                SetInputValue(WaitForElement(By.Id("scriptTimeout")), originalValue.ToString());
                SafeClick(FindScriptTimeoutSaveButton());
                WaitForToast("Tiempo de espera de scripts guardado.");

                GoTo("/configuracion");
                Assert.Equal(originalValue.ToString(), WaitForElement(By.Id("scriptTimeout")).GetAttribute("value"));
            });
    }

    [Theory, TestPriority(1)]
    [InlineData("Batch", "bat", @"C:\Users\italo\Documents\scripts\bat_example.bat")]
    [InlineData("Node.js", "js", @"C:\Users\italo\Documents\scripts\nodejs_script.js")]
    [InlineData("PowerShell", "ps1", @"C:\Users\italo\Documents\scripts\power_example.ps1")]
    public void Scripts_CanCreateExistingScript(string displayType, string type, string path)
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        Assert.True(File.Exists(path), $"El archivo de prueba no existe: {path}");

        SeleniumEvidenceReport.Run(
            $"{nameof(Scripts_CanCreateExistingScript)}_{type}",
            $"Valida que la seccion Scripts permita agregar un script {displayType} existente.",
            _driver!,
            () =>
            {
                Login();
                GoTo("/scripts");
                WaitForHeading("Scripts pre / post copiado");

                var script = new TestScriptInput(
                    $"Selenium {displayType} {DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    type,
                    path);

                CreateScript(script);

                WaitUntilModalCloses();
                WaitForToast("Script creado");
                WaitForScriptInTable(script.Name, ScriptTypeLabel(type), script.Path);
            });
    }

    [Fact, TestPriority(1)]
    public void Scripts_WithMissingPath_ShowsValidationError()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Scripts_WithMissingPath_ShowsValidationError),
            "Valida que no se pueda agregar un script cuya ruta fisica no existe.",
            _driver!,
            () =>
            {
                Login();
                GoTo("/scripts");
                WaitForHeading("Scripts pre / post copiado");

                var missingScript = new TestScriptInput(
                    $"Selenium script inexistente {DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    "ps1",
                    @"C:\Users\italo\Documents\scripts\script_que_no_existe.ps1");

                Assert.False(File.Exists(missingScript.Path), $"La ruta debe ser inexistente para esta prueba: {missingScript.Path}");
                var rowsBefore = CountScriptRows();

                CreateScript(missingScript);

                WaitForToast("No se pudo encontrar la existencia fisica del script en el sistema.");
                Assert.True(WaitForElement(By.CssSelector(".modal")).Displayed);
                Assert.Equal(rowsBefore, CountScriptRows());
                Assert.Empty(_driver!.FindElements(By.XPath($"//tbody//tr[td[normalize-space()={XPathLiteral(missingScript.Name)}]]")));
            });
    }

    [Fact, TestPriority(2)]
    public void Destinations_CanCreateAzureBlobDestination()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Destinations_CanCreateAzureBlobDestination),
            "Valida que la seccion Destinos permita agregar un destino Azure Blob Storage valido.",
            _driver!,
            () =>
            {
                Login();
                GoTo("/destinos");
                WaitForHeading("Destinos");

                var destination = new AzureDestinationInput(
                    $"Selenium Azure {DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    RequiredEnvironment("CLOUDKEEP_AZURE_CONTAINER_NAME"),
                    RequiredEnvironment("CLOUDKEEP_AZURE_CONNECTION_STRING"),
                    "selenium/azure");

                FillAzureDestinationForm(destination);
                TestDestinationConnection();
                WaitForConnectionResult("Conexión correcta");
                SaveDestination();

                WaitUntilModalCloses();
                WaitForToast("Destino creado");
                WaitForAzureDestinationInTable(destination);
                CreatedDestinationName ??= destination.Name;
            });
    }

    [Fact, TestPriority(2)]
    public void Destinations_CanCreateAwsS3Destination()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Destinations_CanCreateAwsS3Destination),
            "Valida que la seccion Destinos permita agregar un destino Amazon S3 valido.",
            _driver!,
            () =>
            {
                Login();
                GoTo("/destinos");
                WaitForHeading("Destinos");

                var destination = new AwsDestinationInput(
                    $"Selenium AWS {DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    RequiredEnvironment("CLOUDKEEP_AWS_BUCKET_NAME"),
                    RequiredEnvironment("CLOUDKEEP_AWS_REGION"),
                    RequiredEnvironment("CLOUDKEEP_AWS_ACCESS_KEY_ID"),
                    RequiredEnvironment("CLOUDKEEP_AWS_SECRET_ACCESS_KEY"),
                    "selenium/aws");

                FillAwsDestinationForm(destination);
                TestDestinationConnection();
                WaitForConnectionResult("Conexión correcta");
                SaveDestination();

                WaitUntilModalCloses();
                WaitForToast("Destino creado");
                WaitForAwsDestinationInTable(destination);
                CreatedDestinationName = destination.Name;
                CreatedAwsDestinationName = destination.Name;
            });
    }

    [Fact, TestPriority(2)]
    public void Destinations_WithInvalidAzureConnection_ShowsValidationError()
    {
        if (!SeleniumTestSettings.IsEnabled)
            return;

        SeleniumEvidenceReport.Run(
            nameof(Destinations_WithInvalidAzureConnection_ShowsValidationError),
            "Valida que no se pueda agregar un destino Azure Blob Storage con cadena de conexion invalida.",
            _driver!,
            () =>
            {
                Login();
                GoTo("/destinos");
                WaitForHeading("Destinos");

                var destination = new AzureDestinationInput(
                    $"Selenium Azure invalido {DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    "contenedor-invalido",
                    "DefaultEndpointsProtocol=https;AccountName=invalid;AccountKey=invalid;EndpointSuffix=core.windows.net",
                    "selenium/invalid");
                var rowsBefore = CountDestinationRows();

                FillAzureDestinationForm(destination);
                SaveDestination();

                WaitForToast("Cadena de conexión de Azure no válida");
                Assert.True(WaitForElement(By.CssSelector(".modal")).Displayed);
                Assert.Equal(rowsBefore, CountDestinationRows());
                Assert.Empty(_driver!.FindElements(By.XPath($"//tbody//tr[td[normalize-space()={XPathLiteral(destination.Name)}]]")));
            });
    }

    private void Login()
    {
        LoginWithPassword(SeleniumTestSettings.Password);
    }

    private void LoginWithPassword(string password)
    {
        GoTo("/login");
        ClearBrowserStorage();
        GoTo("/login");

        PasswordInput().SendKeys(password);
        _driver!.FindElement(By.CssSelector("button[type='submit']")).Click();

        WaitForPath("/");
    }

    private void GoTo(string relativePath)
    {
        _driver!.Navigate().GoToUrl(new Uri(SeleniumTestSettings.BaseUrl, relativePath));
    }

    private void ClearBrowserStorage()
    {
        ((IJavaScriptExecutor)_driver!).ExecuteScript("window.localStorage.clear(); window.sessionStorage.clear();");
    }

    private IWebElement PasswordInput() =>
        _wait!.Until(driver => driver.FindElement(By.Id("password")));

    private IWebElement FindSidebarLink(string linkText) =>
        _wait!.Until(driver => driver.FindElement(By.XPath($"//a[contains(@class,'sidebar-link') and contains(normalize-space(.), '{linkText}')]")));

    private IWebElement FindScriptTimeoutSaveButton() =>
        _wait!.Until(driver => driver.FindElement(By.XPath("//input[@id='scriptTimeout']/ancestor::div[contains(@class,'row')][1]//button")));

    private IWebElement WaitForElement(By by) =>
        _wait!.Until(driver => driver.FindElement(by));

    private void CreateScript(TestScriptInput script)
    {
        SafeClick(_wait!.Until(driver => driver.FindElement(By.XPath("//button[contains(normalize-space(.), 'Nuevo script')]"))));

        var modal = WaitForElement(By.CssSelector(".modal"));
        var inputs = modal.FindElements(By.CssSelector("input.form-control"));
        SetInputValue(inputs[0], script.Name);
        new SelectElement(modal.FindElement(By.CssSelector("select.form-select"))).SelectByValue(script.Type);
        SetInputValue(inputs[1], script.Path);

        SafeClick(modal.FindElement(By.XPath(".//button[contains(normalize-space(.), 'Guardar')]")));
    }

    private void WaitForScriptInTable(string name, string typeLabel, string path)
    {
        _wait!.Until(driver => driver.FindElements(By.XPath(
            $"//tbody//tr[td[normalize-space()={XPathLiteral(name)}] and td//span[normalize-space()={XPathLiteral(typeLabel)}] and td/code[normalize-space()={XPathLiteral(path)}]]")).Count > 0);
    }

    private void FillAzureDestinationForm(AzureDestinationInput destination)
    {
        SafeClick(_wait!.Until(driver => driver.FindElement(By.XPath("//button[contains(normalize-space(.), 'Nuevo destino')]"))));

        var modal = WaitForElement(By.CssSelector(".modal"));
        var inputs = modal.FindElements(By.CssSelector("input.form-control"));
        SetInputValue(inputs[0], destination.Name);
        new SelectElement(modal.FindElement(By.CssSelector("select.form-select"))).SelectByValue("azure_blob");

        _wait!.Until(_ => modal.FindElements(By.CssSelector("textarea.form-control")).Count == 1);
        inputs = modal.FindElements(By.CssSelector("input.form-control"));
        SetInputValue(inputs[1], destination.ContainerName);
        SetInputValue(inputs[2], destination.FolderPrefix);
        SetInputValue(modal.FindElement(By.CssSelector("textarea.form-control")), destination.ConnectionString);
    }

    private void FillAwsDestinationForm(AwsDestinationInput destination)
    {
        SafeClick(_wait!.Until(driver => driver.FindElement(By.XPath("//button[contains(normalize-space(.), 'Nuevo destino')]"))));

        var modal = WaitForElement(By.CssSelector(".modal"));
        var inputs = modal.FindElements(By.CssSelector("input.form-control"));
        SetInputValue(inputs[0], destination.Name);
        SetInputValue(inputs[1], destination.BucketName);
        SetInputValue(inputs[2], destination.Region);
        SetInputValue(inputs[3], destination.FolderPrefix);
        SetInputValue(inputs[4], destination.AccessKeyId);
        SetInputValue(inputs[5], destination.SecretAccessKey);
    }

    private void TestDestinationConnection()
    {
        var modal = WaitForElement(By.CssSelector(".modal"));
        SafeClick(modal.FindElement(By.XPath(".//button[contains(normalize-space(.), 'Probar conexión')]")));
    }

    private void SaveDestination()
    {
        var modal = WaitForElement(By.CssSelector(".modal"));
        SafeClick(modal.FindElement(By.XPath(".//button[contains(normalize-space(.), 'Guardar')]")));
    }

    private void WaitForConnectionResult(string text)
    {
        _wait!.Until(driver => driver.FindElements(By.XPath(
            $"//div[contains(@class, 'modal')]//*[contains(normalize-space(), {XPathLiteral(text)})]")).Count > 0);
    }

    private void WaitForAzureDestinationInTable(AzureDestinationInput destination)
    {
        _wait!.Until(driver => driver.FindElements(By.XPath(
            $"//tbody//tr[td[normalize-space()={XPathLiteral(destination.Name)}] and td//span[normalize-space()='Azure Blob Storage'] and td/code[normalize-space()={XPathLiteral(NormalizeFolderPrefix(destination.FolderPrefix))}] and td/code[normalize-space()={XPathLiteral(destination.ContainerName)}] and td//span[contains(normalize-space(), 'Almacenada')]]")).Count > 0);
    }

    private void WaitForAwsDestinationInTable(AwsDestinationInput destination)
    {
        _wait!.Until(driver => driver.FindElements(By.XPath(
            $"//tbody//tr[td[normalize-space()={XPathLiteral(destination.Name)}] and td//span[normalize-space()='Amazon S3'] and td/code[normalize-space()={XPathLiteral(NormalizeFolderPrefix(destination.FolderPrefix))}] and td/code[normalize-space()={XPathLiteral(destination.BucketName)}] and td/code[normalize-space()={XPathLiteral(destination.Region)}] and td/code[normalize-space()={XPathLiteral(destination.AccessKeyId)}] and td//span[contains(normalize-space(), 'Almacenada')]]")).Count > 0);
    }

    private void CreateJobThroughWizard(string jobName, string jobDescription, string sourceDirectory, DateTime? scheduledUtc, string? destinationName = null)
    {
        GoTo("/trabajos/nuevo");
        WaitForHeading("Nuevo trabajo");

        var generalInputs = _driver!.FindElements(By.CssSelector("input.form-control"));
        SetInputValue(generalInputs[0], jobName);
        SetInputValue(_driver.FindElement(By.CssSelector("textarea.form-control")), jobDescription);
        ClickWizardNext();

        SelectDestinationForJob(destinationName);
        ClickWizardNext();

        WaitForHeading("Origen del respaldo");
        SetInputValue(_driver.FindElement(By.CssSelector("input.font-monospace")), sourceDirectory);
        ValidateSourceFolderInWizard();
        ClickWizardNext();

        WaitForHeading("Programación (Schedule)");
        if (scheduledUtc is not null)
            SetDailySchedule(scheduledUtc.Value);
        ClickWizardNext();

        WaitForHeading("Scripts pre / post (opcional)");
        ClickWizardNext();

        WaitForPath("/trabajos");
        WaitForToast("Trabajo creado");
        WaitForJobInTable(jobName, jobDescription);
    }

    private void SetDailySchedule(DateTime scheduledUtc)
    {
        var selects = _driver!.FindElements(By.CssSelector("select.form-select"));
        new SelectElement(selects[0]).SelectByValue("daily");
        new SelectElement(selects[1]).SelectByText($"{scheduledUtc.Hour:00}:00");

        var minuteInput = _driver.FindElement(By.CssSelector("input[type='number']"));
        SetInputValue(minuteInput, scheduledUtc.Minute.ToString());
    }

    private void SelectDestinationForJob(string? preferredDestinationName = null)
    {
        var select = _wait!.Until(driver =>
        {
            var element = driver.FindElement(By.CssSelector("select.form-select"));
            var options = element.FindElements(By.CssSelector("option"));
            return options.Count > 1 ? element : null;
        });

        var selectElement = new SelectElement(select);
        var destinationName = preferredDestinationName ?? CreatedAwsDestinationName ?? CreatedDestinationName;
        if (!string.IsNullOrWhiteSpace(destinationName))
        {
            selectElement.SelectByText(destinationName);
            return;
        }

        var seleniumDestination = selectElement.Options.FirstOrDefault(option =>
            option.Text.StartsWith("Selenium AWS", StringComparison.OrdinalIgnoreCase))
            ?? selectElement.Options.FirstOrDefault(option =>
                option.Text.StartsWith("Selenium Azure", StringComparison.OrdinalIgnoreCase));

        if (seleniumDestination is not null)
        {
            selectElement.SelectByText(seleniumDestination.Text);
            CreatedDestinationName = seleniumDestination.Text;
            if (seleniumDestination.Text.StartsWith("Selenium AWS", StringComparison.OrdinalIgnoreCase))
                CreatedAwsDestinationName = seleniumDestination.Text;
            return;
        }

        selectElement.SelectByIndex(1);
    }

    private void ClickWizardNext()
    {
        SafeClick(_wait!.Until(driver => driver.FindElement(By.XPath("//button[contains(normalize-space(.), 'Siguiente') or contains(normalize-space(.), 'Finalizar')]"))));
    }

    private void ValidateSourceFolderInWizard()
    {
        SafeClick(_wait!.Until(driver => driver.FindElement(By.XPath("//button[contains(normalize-space(.), 'Comprobar carpeta')]"))));
        _wait.Until(driver => driver.FindElements(By.XPath("//*[contains(normalize-space(), 'Carpeta verificada en el servidor')]")).Count > 0);
    }

    private void WaitForJobInTable(string name, string description)
    {
        var row = By.XPath($"//tbody//tr[td[normalize-space()={XPathLiteral(name)}] and td[normalize-space()={XPathLiteral(description)}] and td//span[normalize-space()='Activo']]");

        FindJobRowAcrossPages(row, name);
    }

    private void ClickRunJob(string jobName)
    {
        var rowBy = By.XPath($"//tbody//tr[td[normalize-space()={XPathLiteral(jobName)}]]");
        var buttonBy = By.XPath(".//button[@aria-label='Ejecutar']");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var row = FindJobRowAcrossPages(rowBy, jobName);
                var runButton = row.FindElement(buttonBy);
                SafeClick(runButton);
                return;
            }
            catch (StaleElementReferenceException) when (attempt < 4)
            {
                Thread.Sleep(500);
            }
            catch (NoSuchElementException) when (attempt < 4)
            {
                Thread.Sleep(500);
            }
        }

        Assert.Fail($"No se pudo presionar Ejecutar para el trabajo: {jobName}");
    }

    private void CreateAzureDestinationAndValidate(AzureDestinationInput destination)
    {
        GoTo("/destinos");
        WaitForHeading("Destinos");
        FillAzureDestinationForm(destination);
        TestDestinationConnection();
        WaitForConnectionResult("Conexión correcta");
        SaveDestination();
        WaitUntilModalCloses();
        WaitForToast("Destino creado");
        WaitForAzureDestinationInTable(destination);
    }

    private void CreateAwsDestinationAndValidate(AwsDestinationInput destination)
    {
        GoTo("/destinos");
        WaitForHeading("Destinos");
        FillAwsDestinationForm(destination);
        TestDestinationConnection();
        WaitForConnectionResult("Conexión correcta");
        SaveDestination();
        WaitUntilModalCloses();
        WaitForToast("Destino creado");
        WaitForAwsDestinationInTable(destination);
    }

    private static CloudDestinationJobInput CreateCloudDestinationJobInput(string destinationName, string label, string suffix)
    {
        var safeLabel = label.Replace(' ', '-').ToLowerInvariant();
        var sourceDirectory = Path.Combine(FindRepositoryRoot(), "TestResults", "Selenium", "JobSources", $"multi-{safeLabel}-{suffix}");
        return new CloudDestinationJobInput(
            $"Selenium Trabajo Multi {label} {suffix}",
            $"Trabajo multi destino {label} creado por Selenium.",
            destinationName,
            sourceDirectory);
    }

    private IWebElement FindJobRowAcrossPages(By row, string jobName)
    {
        for (var page = 0; page < 20; page++)
        {
            var rows = _driver!.FindElements(row);
            if (rows.Count > 0)
                return rows[0];

            var nextButtons = _driver.FindElements(By.XPath("//button[@aria-label='Página siguiente' and not(@disabled)]"));
            if (nextButtons.Count == 0)
                break;

            SafeClick(nextButtons[0]);
            _wait!.Until(driver => driver.FindElements(By.CssSelector("tbody tr")).Count > 0);
        }

        Assert.Fail($"No se encontro el trabajo creado en la tabla: {jobName}");
        throw new InvalidOperationException($"No se encontro el trabajo creado en la tabla: {jobName}");
    }

    private void WaitForManualExecutionCompletedToast()
    {
        var executionWait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(120));
        executionWait.Until(driver =>
        {
            var toastMessages = driver
                .FindElements(By.CssSelector(".toast-content"))
                .Select(element => element.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            var error = toastMessages.FirstOrDefault(text =>
                text.Contains("fall", StringComparison.OrdinalIgnoreCase)
                || text.Contains("no existe", StringComparison.OrdinalIgnoreCase)
                || text.Contains("error", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException($"La ejecución manual falló: {error}");

            return toastMessages.Any(text =>
                text.Contains("Ejecución correcta", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Ejecución finalizada", StringComparison.OrdinalIgnoreCase));
        });
    }

    private void WaitForManualExecutionReport(string jobName)
    {
        var reportWait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(120));
        reportWait.Until(driver => driver.FindElements(By.XPath(
            $"//tr[td[normalize-space()={XPathLiteral(jobName)}] and td//span[normalize-space()='Completado'] and td//span[normalize-space()='Manual']]")).Count > 0);
    }

    private void WaitForScheduledExecutionReport(string jobName)
    {
        var deadline = DateTime.UtcNow.AddMinutes(4);
        while (DateTime.UtcNow < deadline)
        {
            FilterExecutionReportByJob(jobName);
            if (_driver!.FindElements(By.XPath(
                $"//tr[td[normalize-space()={XPathLiteral(jobName)}] and td//span[normalize-space()='Completado'] and td//span[contains(normalize-space(), 'Programada')]]")).Count > 0)
            {
                return;
            }

            Thread.Sleep(TimeSpan.FromSeconds(10));
            _driver.Navigate().Refresh();
            WaitForHeading("Reporte de trabajos");
        }

        Assert.Fail($"No se encontro la ejecucion automatica completada del trabajo en reportes: {jobName}");
    }

    private static void WaitForScheduledExecutionTime(DateTime scheduledUtc)
    {
        var target = scheduledUtc.AddSeconds(75);
        var remaining = target - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero)
            Thread.Sleep(remaining);
    }

    private void FilterExecutionReportByJob(string jobName)
    {
        var reportWait = new WebDriverWait(_driver!, TimeSpan.FromSeconds(30));
        var jobFilter = reportWait.Until(driver =>
        {
            var selects = driver.FindElements(By.CssSelector("select.form-select-sm"));
            if (selects.Count == 0)
                return null;

            var select = new SelectElement(selects[0]);
            return select.Options.Any(option => option.Text.Equals(jobName, StringComparison.Ordinal))
                ? selects[0]
                : null;
        });

        new SelectElement(jobFilter).SelectByText(jobName);
        SafeClick(_driver!.FindElement(By.XPath("//button[contains(normalize-space(), 'Aplicar filtro')]")));
        reportWait.Until(driver => driver.FindElements(By.XPath("//td[normalize-space()={0}]".Replace("{0}", XPathLiteral(jobName)))).Count > 0);
    }

    private static void EnsureJobSourceDirectory(string sourceDirectory)
    {
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "archivo-prueba.txt"), $"Contenido de prueba Selenium {DateTime.UtcNow:O}.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProyectoDeGrado.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    private void WaitUntilModalCloses()
    {
        _wait!.Until(driver => driver.FindElements(By.CssSelector(".modal")).Count == 0);
    }

    private int CountScriptRows() =>
        _driver!.FindElements(By.XPath("//tbody//tr[td[not(@colspan)]]")).Count;

    private int CountDestinationRows() =>
        _driver!.FindElements(By.XPath("//tbody//tr[td[not(@colspan)]]")).Count;

    private static string ScriptTypeLabel(string type) =>
        type switch
        {
            "ps1" => "PowerShell (.ps1)",
            "bat" => "Batch (.bat)",
            "js" => "Node.js (.js)",
            _ => type,
        };

    private static string RequiredEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value), $"Configure la variable de entorno {name} para ejecutar esta prueba Selenium.");
        return value;
    }

    private static string NormalizeFolderPrefix(string value)
    {
        var normalized = string.Join('/', value.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrEmpty(normalized) ? string.Empty : $"{normalized}/";
    }

    private void SetInputValue(IWebElement input, string value)
    {
        input.Clear();
        input.SendKeys(value);
    }

    private void SafeClick(IWebElement element)
    {
        ((IJavaScriptExecutor)_driver!).ExecuteScript("arguments[0].scrollIntoView({ block: 'center', inline: 'center' });", element);
        try
        {
            element.Click();
        }
        catch (ElementClickInterceptedException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", element);
        }
    }

    private void SafeClickRetry(IWebElement element, Func<IWebElement> refreshElement)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                SafeClick(element);
                return;
            }
            catch (StaleElementReferenceException) when (attempt < 2)
            {
                element = refreshElement();
            }
        }
    }

    private void WaitForToast(string text)
    {
        _wait!.Until(driver => driver
            .FindElements(By.CssSelector(".toast-content"))
            .Any(element => element.Text.Contains(text, StringComparison.OrdinalIgnoreCase)));
    }

    private void WaitForPath(string expectedPath)
    {
        _wait!.Until(driver => driver.UrlAsUri().AbsolutePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase));
    }

    private void WaitForHeading(string text)
    {
        _wait!.Until(driver => driver.FindElement(By.XPath($"//*[self::h1 or self::h2 or self::h3 or self::h4][normalize-space()='{text}']")).Displayed);
    }

    private static string XPathLiteral(string value)
    {
        if (!value.Contains('\''))
            return $"'{value}'";

        if (!value.Contains('"'))
            return $"\"{value}\"";

        return "concat('" + value.Replace("'", "', \"'\", '") + "')";
    }

    public void Dispose()
    {
        _driver?.Quit();
    }

    private sealed record TestScriptInput(string Name, string Type, string Path);

    private sealed record AzureDestinationInput(string Name, string ContainerName, string ConnectionString, string FolderPrefix);

    private sealed record AwsDestinationInput(
        string Name,
        string BucketName,
        string Region,
        string AccessKeyId,
        string SecretAccessKey,
        string FolderPrefix);

    private sealed record CloudDestinationJobInput(
        string Name,
        string Description,
        string DestinationName,
        string SourceDirectory);
}

internal static class SeleniumTestSettings
{
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("CLOUDKEEP_E2E"), "1", StringComparison.Ordinal);

    public static Uri BaseUrl =>
        new(Environment.GetEnvironmentVariable("CLOUDKEEP_BASE_URL") ?? "http://localhost:5271");

    public static string Password =>
        Environment.GetEnvironmentVariable("CLOUDKEEP_PASSWORD") ?? "admin";

    public static string AppExePath =>
        Environment.GetEnvironmentVariable("CLOUDKEEP_APP_EXE")
        ?? Path.Combine(FindRepositoryRoot(), "API", "bin", "Release", "net10.0-windows", "win-x64", "publish", "CloudKeep.exe");

    public static bool Headless =>
        !string.Equals(Environment.GetEnvironmentVariable("CLOUDKEEP_HEADLESS"), "false", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProyectoDeGrado.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}

internal static class WebDriverUrlExtensions
{
    public static Uri UrlAsUri(this IWebDriver driver) => new(driver.Url);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

public sealed class PriorityTestCaseOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        return testCases.OrderBy(GetPriority).ThenBy(testCase => testCase.TestMethod.Method.Name);
    }

    private static int GetPriority<TTestCase>(TTestCase testCase)
        where TTestCase : ITestCase
    {
        var attribute = testCase.TestMethod.Method
            .GetCustomAttributes(typeof(TestPriorityAttribute).AssemblyQualifiedName!)
            .FirstOrDefault();

        return attribute?.GetNamedArgument<int>(nameof(TestPriorityAttribute.Priority)) ?? 100;
    }
}
