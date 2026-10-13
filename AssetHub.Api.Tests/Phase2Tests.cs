using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssetHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Tests;

internal static class TestApi
{
    public static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    public static MultipartFormDataContent Form(params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in fields)
            form.Add(new StringContent(value), name);
        return form;
    }

    /// <summary>A complete device form. <paramref name="serial"/> defaults to a new unique serial; pass "" for none.</summary>
    public static MultipartFormDataContent DeviceForm(string type, string? ram = "16GB", string? storage = "512GB SSD", string? cpu = "Core i7",
        string? serial = null, string? screenSize = null) =>
        Form(
            ("DeviceName", Unique("Device ")),
            ("SerialNumber", serial ?? Unique("SN-")),
            ("ScreenSizeInches", screenSize ?? ""),
            ("DeviceType", type),
            ("Platform", "Windows"),
            ("Brand", "Dell"),
            ("Model", "Model"),
            ("Ram", ram ?? ""),
            ("Storage", storage ?? ""),
            ("Cpu", cpu ?? ""),
            ("Status", "Available"));

    /// <summary><paramref name="employeeId"/> defaults to a new unique ID; pass "" for none.</summary>
    public static MultipartFormDataContent EmployeeForm(string fullName, string email, string? employeeId = null) =>
        Form(("FullName", fullName), ("Email", email), ("EmployeeId", employeeId ?? Unique("E-")), ("Department", "IT"), ("JobTitle", "Engineer"));

    public static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["message"].ToString()!;

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, string key)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return body![key].Deserialize<T>()!;
    }

    /// <summary>Creates a system user with the given role (as SuperAdmin) and returns a client signed in as them.</summary>
    public static async Task<HttpClient> ClientWithRoleAsync(ApiFactory factory, string role)
    {
        var superAdmin = await factory.CreateClientAsync();
        var userName = Unique(role.ToLowerInvariant() + "_");
        const string password = "Role-Pass-123";
        var create = await superAdmin.PostAsJsonAsync("/api/SystemUsers",
            new { fullName = role + " user", userName, email = "", department = "", role, password });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        return await factory.CreateClientAsync(userName, password);
    }
}

[Collection(ApiCollection.Name)]
public class SupportITRoleTests
{
    private readonly ApiFactory _factory;

    public SupportITRoleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SupportIT_CanCreateAndEditEverything()
    {
        var support = await TestApi.ClientWithRoleAsync(_factory, "SupportIT");

        // Device: create, edit, status
        var deviceCode = await TestApi.ReadAsync<string>(await support.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop")), "deviceCode");
        Assert.Equal(HttpStatusCode.OK, (await support.PutAsync($"/api/Devices/by-code/{deviceCode}", TestApi.DeviceForm("Laptop"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await support.GetAsync("/api/Devices/check-serial?serial=X")).StatusCode);
        await using (var db = _factory.CreateDbContext())
        {
            var deviceId = await db.Devices.Where(x => x.DeviceCode == deviceCode).Select(x => x.Id).SingleAsync();
            Assert.Equal(HttpStatusCode.OK, (await support.PutAsJsonAsync($"/api/Devices/{deviceId}/status", "Maintenance")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await support.PutAsJsonAsync($"/api/Devices/{deviceId}/status", "Available")).StatusCode);
        }

        // Employee: create, edit, deactivate + activate
        var employeeId = await TestApi.ReadAsync<int>(await support.PostAsync("/api/Users", TestApi.EmployeeForm("Support Made", "")), "id");
        Assert.Equal(HttpStatusCode.OK, (await support.PutAsync($"/api/Users/{employeeId}", TestApi.EmployeeForm("Support Edited", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await support.PutAsync($"/api/Users/{employeeId}/toggle-active", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await support.PutAsync($"/api/Users/{employeeId}/toggle-active", null)).StatusCode);

        // Assignment: assign + return
        int assignmentDeviceId;
        await using (var db = _factory.CreateDbContext())
            assignmentDeviceId = await db.Devices.Where(x => x.DeviceCode == deviceCode).Select(x => x.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.OK, (await support.PostAsJsonAsync("/api/Assignments", new { userId = employeeId, deviceId = assignmentDeviceId })).StatusCode);
        await using (var db = _factory.CreateDbContext())
        {
            var assignmentId = await db.Assignments.Where(x => x.DeviceId == assignmentDeviceId).Select(x => x.Id).SingleAsync();
            Assert.Equal(HttpStatusCode.OK, (await support.PutAsync($"/api/Assignments/{assignmentId}/return", null)).StatusCode);
        }

        // Department and device type: create + edit
        var departmentId = await TestApi.ReadAsync<int>(
            await support.PostAsJsonAsync("/api/Departments", new { departmentName = TestApi.Unique("Dept "), managerName = "M" }), "id");
        Assert.Equal(HttpStatusCode.OK,
            (await support.PutAsJsonAsync($"/api/Departments/{departmentId}", new { departmentName = TestApi.Unique("Dept "), managerName = "M2" })).StatusCode);

        var typeId = await TestApi.ReadAsync<int>(
            await support.PostAsJsonAsync("/api/DeviceTypes", new { typeName = TestApi.Unique("Type "), icon = "x" }), "id");
        Assert.Equal(HttpStatusCode.OK,
            (await support.PutAsJsonAsync($"/api/DeviceTypes/{typeId}", new { typeName = TestApi.Unique("Type "), icon = "y" })).StatusCode);
    }

    [Fact]
    public async Task SupportIT_Gets403OnEveryDelete_AndNothingIsDeleted()
    {
        var superAdmin = await _factory.CreateClientAsync();
        var support = await TestApi.ClientWithRoleAsync(_factory, "SupportIT");

        var deviceCode = await TestApi.ReadAsync<string>(await superAdmin.PostAsync("/api/Devices", TestApi.DeviceForm("Bag")), "deviceCode");
        var employeeId = await TestApi.ReadAsync<int>(await superAdmin.PostAsync("/api/Users", TestApi.EmployeeForm("To keep", "")), "id");
        var departmentId = await TestApi.ReadAsync<int>(
            await superAdmin.PostAsJsonAsync("/api/Departments", new { departmentName = TestApi.Unique("Dept "), managerName = "" }), "id");
        var typeId = await TestApi.ReadAsync<int>(
            await superAdmin.PostAsJsonAsync("/api/DeviceTypes", new { typeName = TestApi.Unique("Type "), icon = "" }), "id");

        var deletes = new[]
        {
            $"/api/Devices/by-code/{deviceCode}",
            $"/api/Users/{employeeId}",
            $"/api/Departments/{departmentId}",
            $"/api/DeviceTypes/{typeId}",
            "/api/AuditLogs/clear",
            "/api/SystemUsers/1"
        };

        foreach (var url in deletes)
            Assert.True((await support.DeleteAsync(url)).StatusCode == HttpStatusCode.Forbidden, $"DELETE {url} should be 403");

        Assert.Equal(HttpStatusCode.OK, (await support.GetAsync($"/api/Devices/by-code/{deviceCode}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await support.GetAsync($"/api/Users/{employeeId}")).StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.True(await db.Departments.AnyAsync(x => x.Id == departmentId));
        Assert.True(await db.DeviceTypes.AnyAsync(x => x.Id == typeId));
        Assert.True(await db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task SupportIT_CannotManageSystemUsers()
    {
        var support = await TestApi.ClientWithRoleAsync(_factory, "SupportIT");

        Assert.Equal(HttpStatusCode.Forbidden, (await support.GetAsync("/api/SystemUsers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostAsJsonAsync("/api/SystemUsers",
            new { fullName = "X", userName = TestApi.Unique("x_"), email = "", department = "", role = "Admin", password = "Some-Pass-1" })).StatusCode);
    }

    [Fact]
    public async Task SupportIT_CanReadEverything()
    {
        var support = await TestApi.ClientWithRoleAsync(_factory, "SupportIT");

        foreach (var url in new[] { "/api/Dashboard", "/api/Devices", "/api/Users", "/api/Assignments", "/api/Departments", "/api/DeviceTypes", "/api/Reports", "/api/AuditLogs" })
            Assert.True((await support.GetAsync(url)).StatusCode == HttpStatusCode.OK, $"GET {url} should be 200");
    }

    [Fact]
    public async Task Admin_CanStillDelete()
    {
        var admin = await TestApi.ClientWithRoleAsync(_factory, "Admin");
        var deviceCode = await TestApi.ReadAsync<string>(await admin.PostAsync("/api/Devices", TestApi.DeviceForm("Bag")), "deviceCode");

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/Devices/by-code/{deviceCode}")).StatusCode);
    }

}

[Collection(ApiCollection.Name)]
public class OptionalEmployeeEmailTests
{
    private readonly ApiFactory _factory;

    public OptionalEmployeeEmailTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<AppUser> LoadAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    [Fact]
    public async Task Create_WithoutEmail_StoresNull_AndDerivesUserNameFromEmployeeId()
    {
        var client = await _factory.CreateClientAsync();
        var employeeCode = TestApi.Unique("E-");

        var id = await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("No Email", "   ", employeeCode)), "id");

        var user = await LoadAsync(id);
        Assert.Null(user.Email);
        Assert.Equal(employeeCode, user.UserName);

        var json = await client.GetFromJsonAsync<Dictionary<string, object?>>($"/api/Users/{id}");
        Assert.Null(json!["email"]);
    }

    [Fact]
    public async Task ManyEmployeesWithoutEmail_AreAllowed()
    {
        var client = await _factory.CreateClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/Users", TestApi.EmployeeForm("Blank 1", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/Users", TestApi.EmployeeForm("Blank 2", ""))).StatusCode);
    }

    [Fact]
    public async Task DuplicateEmail_IsStillRejected_AndUserNameUsesTheEmailPrefix()
    {
        var client = await _factory.CreateClientAsync();
        var prefix = TestApi.Unique("mail");
        var email = $"{prefix}@example.com";

        var id = await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("Has Email", $" {email} ")), "id");
        var duplicate = await client.PostAsync("/api/Users", TestApi.EmployeeForm("Same Email", email));

        Assert.Equal(prefix, (await LoadAsync(id)).UserName);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("Email already exists", await TestApi.MessageAsync(duplicate));
    }

    [Fact]
    public async Task Update_ClearingTheEmail_StoresNull()
    {
        var client = await _factory.CreateClientAsync();
        var employeeCode = TestApi.Unique("E-");
        var id = await TestApi.ReadAsync<int>(
            await client.PostAsync("/api/Users", TestApi.EmployeeForm("Clear Me", $"{TestApi.Unique("c")}@example.com", employeeCode)), "id");

        var response = await client.PutAsync($"/api/Users/{id}", TestApi.EmployeeForm("Clear Me", "", employeeCode));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await LoadAsync(id);
        Assert.Null(user.Email);
        Assert.Equal(employeeCode, user.UserName);
    }
}

[Collection(ApiCollection.Name)]
public class RequiredHardwareTests
{
    private readonly ApiFactory _factory;

    public RequiredHardwareTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Laptop_WithoutCpu_IsRejected()
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop", cpu: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("RAM, Storage and CPU are required for Laptop devices (N/A is not accepted). Missing: CPU.", await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task Desktop_TypeIsCaseInsensitive_AndWhitespaceCountsAsMissing()
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm(" desktop ", ram: "   ", storage: null, cpu: "i5"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("RAM, Storage and CPU are required for desktop devices (N/A is not accepted). Missing: RAM, Storage.", await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task Bag_WithoutCpuRamOrStorage_IsAccepted()
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm("Bag", ram: null, storage: null, cpu: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_LaptopRemovingCpu_IsRejected()
    {
        var client = await _factory.CreateClientAsync();
        var code = await TestApi.ReadAsync<string>(await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop")), "deviceCode");

        var response = await client.PutAsync($"/api/Devices/by-code/{code}", TestApi.DeviceForm("LAPTOP", cpu: " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Missing: CPU.", await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task Update_ChangingTypeToBag_DropsTheRequirement()
    {
        var client = await _factory.CreateClientAsync();
        var code = await TestApi.ReadAsync<string>(await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop")), "deviceCode");

        var response = await client.PutAsync($"/api/Devices/by-code/{code}", TestApi.DeviceForm("Bag", ram: null, storage: null, cpu: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
