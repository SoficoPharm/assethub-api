using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssetHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Tests;

[Collection(ApiCollection.Name)]
public class RequiredEmployeeIdTests
{
    private readonly ApiFactory _factory;

    public RequiredEmployeeIdTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutEmployeeId_Returns400(string employeeId)
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Users", TestApi.EmployeeForm("No ID", "", employeeId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Employee ID is required", await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task Create_TrimsTheEmployeeId()
    {
        var client = await _factory.CreateClientAsync();
        var employeeId = TestApi.Unique("E-");

        var id = await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("Trimmed", "", $"  {employeeId} ")), "id");

        await using var db = _factory.CreateDbContext();
        Assert.Equal(employeeId, (await db.Users.SingleAsync(x => x.Id == id)).EmployeeId);
    }

    [Fact]
    public async Task Create_DuplicateEmployeeId_IgnoringCase_Returns409()
    {
        var client = await _factory.CreateClientAsync();
        var employeeId = TestApi.Unique("e-abc-");
        await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("First", "", employeeId)), "id");

        var duplicate = employeeId.ToUpperInvariant();
        var response = await client.PostAsync("/api/Users", TestApi.EmployeeForm("Second", "", $" {duplicate} "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"Employee ID {duplicate} already exists", await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task Update_WithoutEmployeeId_Returns400_AndKeepsTheOldId()
    {
        var client = await _factory.CreateClientAsync();
        var employeeId = TestApi.Unique("E-");
        var id = await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("Keep ID", "", employeeId)), "id");

        var response = await client.PutAsync($"/api/Users/{id}", TestApi.EmployeeForm("Keep ID", "", " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Employee ID is required", await TestApi.MessageAsync(response));
        await using var db = _factory.CreateDbContext();
        Assert.Equal(employeeId, (await db.Users.SingleAsync(x => x.Id == id)).EmployeeId);
    }

    [Fact]
    public async Task Update_ToAnotherEmployeesId_Returns409_ButKeepingItsOwnIdSucceeds()
    {
        var client = await _factory.CreateClientAsync();
        var takenId = TestApi.Unique("E-");
        await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("Owner", "", takenId)), "id");
        var ownId = TestApi.Unique("E-");
        var id = await TestApi.ReadAsync<int>(await client.PostAsync("/api/Users", TestApi.EmployeeForm("Other", "", ownId)), "id");

        var conflict = await client.PutAsync($"/api/Users/{id}", TestApi.EmployeeForm("Other", "", takenId.ToLowerInvariant()));
        var keep = await client.PutAsync($"/api/Users/{id}", TestApi.EmployeeForm("Other renamed", "", ownId.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal($"Employee ID {takenId.ToLowerInvariant()} already exists", await TestApi.MessageAsync(conflict));
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class RequiredDeviceFieldsTests
{
    private readonly ApiFactory _factory;

    public RequiredDeviceFieldsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Create_WithoutDeviceType_Returns400(string type)
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm(type));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Device type is required", await TestApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("Laptop")]
    [InlineData("Mobile")]
    [InlineData("Printer")]
    public async Task Create_WithoutSerial_Returns400_ForTypesThatNeedOne(string type)
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm(type, serial: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal($"Serial number is required for {type} devices", await TestApi.MessageAsync(response));
    }

    [Theory]
    [InlineData("Bag")]
    [InlineData("bag")]
    [InlineData("SIM Line")]
    [InlineData(" sim line ")]
    public async Task Create_WithoutSerial_IsAccepted_ForBagAndSimLine(string type)
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm(type, ram: null, storage: null, cpu: null, serial: ""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("N/A", "512 GB", "i7", "RAM")]
    [InlineData("16 GB", " n/a ", "i7", "Storage")]
    [InlineData("16 GB", "512 GB", "N/A", "CPU")]
    public async Task Laptop_WithNA_IsRejected(string ram, string storage, string cpu, string missing)
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop", ram, storage, cpu));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.EndsWith($"Missing: {missing}.", await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task OtherTypes_CanStillUseNA()
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm("Mobile", ram: "N/A", storage: "N/A", cpu: "N/A"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ExistingDeviceWithoutSerial_RequiresOne()
    {
        // An old device saved before serials were required.
        var code = "OLD-" + Guid.NewGuid().ToString("N")[..8];
        await using (var db = _factory.CreateDbContext())
        {
            db.Devices.Add(new Device { DeviceCode = code, DeviceName = "Old phone", DeviceType = "Mobile", SerialNumber = null });
            await db.SaveChangesAsync();
        }

        var client = await _factory.CreateClientAsync();
        var withoutSerial = await client.PutAsync($"/api/Devices/by-code/{code}", TestApi.DeviceForm("Mobile", serial: ""));
        var withSerial = await client.PutAsync($"/api/Devices/by-code/{code}", TestApi.DeviceForm("Mobile"));

        Assert.Equal(HttpStatusCode.BadRequest, withoutSerial.StatusCode);
        Assert.Equal("Serial number is required for Mobile devices", await TestApi.MessageAsync(withoutSerial));
        Assert.Equal(HttpStatusCode.OK, withSerial.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class ScreenSizeTests
{
    private readonly ApiFactory _factory;

    public ScreenSizeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<decimal?> ScreenSizeAsync(HttpClient client, string code)
    {
        var device = await client.GetFromJsonAsync<Dictionary<string, JsonElement>>($"/api/Devices/by-code/{code}");
        var value = device!["screenSizeInches"];
        return value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
    }

    [Theory]
    [InlineData("15.6", 15.6)]
    [InlineData(" 7 ", 7)]
    [InlineData("100", 100)]
    [InlineData("13.0", 13)]
    public async Task ValidSize_IsStoredAndReturned(string input, double expected)
    {
        var client = await _factory.CreateClientAsync();

        var code = await TestApi.ReadAsync<string>(await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop", screenSize: input)), "deviceCode");

        Assert.Equal((decimal)expected, await ScreenSizeAsync(client, code));
    }

    [Fact]
    public async Task EmptySize_IsNull_AndUpdateCanSetAndClearIt()
    {
        var client = await _factory.CreateClientAsync();
        var code = await TestApi.ReadAsync<string>(await client.PostAsync("/api/Devices", TestApi.DeviceForm("Desktop", screenSize: "")), "deviceCode");
        Assert.Null(await ScreenSizeAsync(client, code));

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/Devices/by-code/{code}", TestApi.DeviceForm("Desktop", screenSize: "27"))).StatusCode);
        Assert.Equal(27m, await ScreenSizeAsync(client, code));

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/Devices/by-code/{code}", TestApi.DeviceForm("Desktop", screenSize: ""))).StatusCode);
        Assert.Null(await ScreenSizeAsync(client, code));
    }

    [Theory]
    [InlineData("6.9", "Screen size must be between 7 and 100 inches")]
    [InlineData("100.1", "Screen size must be between 7 and 100 inches")]
    [InlineData("-15", "Screen size must be a number, e.g. 15.6")]
    [InlineData("15.65", "Screen size can have at most one decimal, e.g. 15.6")]
    [InlineData("15,6", "Screen size must be a number, e.g. 15.6")]
    [InlineData("big", "Screen size must be a number, e.g. 15.6")]
    public async Task InvalidSize_Returns400(string input, string message)
    {
        var client = await _factory.CreateClientAsync();

        var response = await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop", screenSize: input));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await TestApi.MessageAsync(response));
    }

    [Fact]
    public async Task Size_IsInTheDevicesList()
    {
        var client = await _factory.CreateClientAsync();
        var code = await TestApi.ReadAsync<string>(await client.PostAsync("/api/Devices", TestApi.DeviceForm("Laptop", screenSize: "14.0")), "deviceCode");

        var devices = await client.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>("/api/Devices");

        Assert.Equal(14m, devices!.Single(d => d["code"].GetString() == code)["screenSizeInches"].GetDecimal());
    }
}
