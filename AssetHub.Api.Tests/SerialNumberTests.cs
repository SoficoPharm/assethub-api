using System.Net;
using System.Net.Http.Json;
using AssetHub.Api.Models;
using AssetHub.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AssetHub.Api.Tests;

public class SerialNumberNormalizeTests
{
    [Theory]
    [InlineData("CL7WNL3", "CL7WNL3")]
    [InlineData("  cl7wnl3 ", "CL7WNL3")]
    [InlineData("\tAb-12\n", "AB-12")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_TrimsUpperCasesAndTreatsEmptyAsNull(string? input, string? expected) =>
        Assert.Equal(expected, SerialNumbers.Normalize(input));
}

public class SerialNumberApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SerialNumberApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string NewSerial() => "SN-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private static MultipartFormDataContent DeviceForm(string name, string? serial) => new()
    {
        { new StringContent(name), "DeviceName" },
        { new StringContent(serial ?? ""), "SerialNumber" },
        { new StringContent("Laptop"), "DeviceType" },
        { new StringContent("Windows"), "Platform" },
        { new StringContent("Dell"), "Brand" },
        { new StringContent("Latitude"), "Model" },
        { new StringContent("Available"), "Status" }
    };

    private static async Task<string> CreateDeviceAsync(HttpClient client, string name, string? serial)
    {
        var response = await client.PostAsync("/api/Devices", DeviceForm(name, serial));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return body!["deviceCode"].ToString()!;
    }

    private static async Task<string?> GetSerialAsync(HttpClient client, string code)
    {
        var device = await client.GetFromJsonAsync<Dictionary<string, object?>>($"/api/Devices/by-code/{code}");
        return device!["serialNumber"]?.ToString();
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"];

    [Fact]
    public async Task Create_StoresTheNormalizedSerial()
    {
        var client = await _factory.CreateClientAsync();
        var serial = NewSerial();

        var code = await CreateDeviceAsync(client, "Normalized", $"  {serial.ToLowerInvariant()} ");

        Assert.Equal(serial, await GetSerialAsync(client, code));
    }

    [Fact]
    public async Task Create_DuplicateWithDifferentCaseAndSpaces_Returns409WithOwner()
    {
        var client = await _factory.CreateClientAsync();
        var serial = NewSerial();
        var ownerCode = await CreateDeviceAsync(client, "Original laptop", serial);

        var response = await client.PostAsync("/api/Devices", DeviceForm("Copy", $" {serial.ToLowerInvariant()}  "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"Serial number {serial} already exists on device {ownerCode} (Original laptop)", await MessageAsync(response));
    }

    [Fact]
    public async Task Create_WithoutSerial_IsAllowedForManyDevices()
    {
        var client = await _factory.CreateClientAsync();

        var first = await CreateDeviceAsync(client, "No serial 1", "");
        var second = await CreateDeviceAsync(client, "No serial 2", "   ");

        Assert.Null(await GetSerialAsync(client, first));
        Assert.Null(await GetSerialAsync(client, second));
    }

    [Fact]
    public async Task Update_ToAnotherDevicesSerial_Returns409()
    {
        var client = await _factory.CreateClientAsync();
        var takenSerial = NewSerial();
        var ownerCode = await CreateDeviceAsync(client, "Owner", takenSerial);
        var otherCode = await CreateDeviceAsync(client, "Other", NewSerial());

        var response = await client.PutAsync($"/api/Devices/by-code/{otherCode}", DeviceForm("Other", takenSerial.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal($"Serial number {takenSerial} already exists on device {ownerCode} (Owner)", await MessageAsync(response));
    }

    [Fact]
    public async Task Update_KeepingItsOwnSerial_Succeeds()
    {
        var client = await _factory.CreateClientAsync();
        var serial = NewSerial();
        var code = await CreateDeviceAsync(client, "Mine", serial);

        var response = await client.PutAsync($"/api/Devices/by-code/{code}", DeviceForm("Mine renamed", $" {serial.ToLowerInvariant()}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(serial, await GetSerialAsync(client, code));
    }

    [Fact]
    public async Task CheckSerial_ReportsTheOwner_AndHonoursExcludeCode()
    {
        var client = await _factory.CreateClientAsync();
        var serial = NewSerial();
        var code = await CreateDeviceAsync(client, "Checked device", serial);

        var found = await client.GetFromJsonAsync<SerialCheck>($"/api/Devices/check-serial?serial=%20{serial.ToLowerInvariant()}");
        var excluded = await client.GetFromJsonAsync<SerialCheck>($"/api/Devices/check-serial?serial={serial}&excludeCode={code}");
        var empty = await client.GetFromJsonAsync<SerialCheck>("/api/Devices/check-serial?serial=%20%20");

        Assert.Equal(new SerialCheck(true, code, "Checked device"), found);
        Assert.Equal(new SerialCheck(false, null, null), excluded);
        Assert.Equal(new SerialCheck(false, null, null), empty);
    }

    [Fact]
    public async Task CheckSerial_IsForbiddenForViewers()
    {
        var admin = await _factory.CreateClientAsync();
        var userName = "viewer_" + Guid.NewGuid().ToString("N")[..8];
        var create = await admin.PostAsJsonAsync("/api/SystemUsers",
            new { fullName = "Viewer", userName, email = "", department = "", role = "Viewer", password = "Viewer-Pass-1" });
        create.EnsureSuccessStatusCode();

        var viewer = await _factory.CreateClientAsync(userName, "Viewer-Pass-1");
        var response = await viewer.GetAsync($"/api/Devices/check-serial?serial={NewSerial()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentUpdates_ToTheSameSerial_OneWinsAndTheRestGet409()
    {
        var client = await _factory.CreateClientAsync();
        var codes = new List<string>();
        for (var i = 0; i < 6; i++)
            codes.Add(await CreateDeviceAsync(client, $"Racer {i}", NewSerial()));

        var contested = NewSerial();

        // Updates do not generate device codes, so the only thing they can collide on is the serial.
        // Requests that pass the pre-check together hit the unique index and must still return 409.
        var responses = await Task.WhenAll(codes.Select(code =>
            client.PutAsync($"/api/Devices/by-code/{code}", DeviceForm("Racer", contested))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(codes.Count - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.StartsWith($"Serial number {contested} already exists on device DEV-", await MessageAsync(conflict));
    }

    [Fact]
    public async Task Database_UniqueIndex_RejectsDuplicates_ButAllowsNulls()
    {
        await using var db = _factory.CreateDbContext();
        var serial = NewSerial();
        Device NewDevice(string? s) => new()
        {
            DeviceCode = "IDX-" + Guid.NewGuid().ToString("N")[..8],
            DeviceName = "Index test",
            SerialNumber = s
        };

        db.Devices.AddRange(NewDevice(null), NewDevice(null), NewDevice(serial));
        await db.SaveChangesAsync();

        db.Devices.Add(NewDevice(serial));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.True(SerialNumbers.IsUniqueViolation(ex));
    }

    private record SerialCheck(bool Exists, string? DeviceCode, string? DeviceName);
}
