namespace AquaMonitor.BddTests.StepDefinitions;

[Binding]
public sealed class AuthenticationSteps(ScenarioWorld world)
{
    [Given("que ainda não possuo uma conta")]
    public void NewAccount() => world.Actor = world.App.Client();

    [When("cadastro meus dados válidos")]
    public async Task Register() => world.Response = await world.Actor.PostAsJsonAsync("/api/Auth/register",
        new { name = "Pessoa", email = world.Email, password = ApiScenario.Password });

    [Then("minha conta deve ser criada como usuário comum")]
    public async Task Registered()
    {
        Assert.Equal(HttpStatusCode.Created, world.Response.StatusCode);
        var body = await ApiScenario.Json(world.Response);
        Assert.Equal("User", body.GetProperty("role").GetString());
        Assert.Equal(world.Email, body.GetProperty("email").GetString());
        Assert.False(body.TryGetProperty("password", out _));
    }

    [When("entro com minha senha correta")]
    public async Task Login() => await ApiScenario.Login(world.Actor, world.Email);

    [Then("devo conseguir consultar meus sensores")]
    public async Task SessionWorks()
    {
        using var response = await world.Actor.GetAsync("/api/WaterSensors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ApiScenario.Json(response)).GetProperty("data").EnumerateArray());
    }

    [Given("que possuo uma conta cadastrada")]
    public async Task ExistingAccount()
    {
        world.Actor = await world.App.User(world.Email);
        world.Actor.DefaultRequestHeaders.Authorization = null;
    }

    [When("tento entrar com uma senha incorreta")]
    public async Task WrongPassword() => world.Response = await world.Actor.PostAsJsonAsync("/api/Auth/login",
        new { email = world.Email, password = "senha-incorreta" });

    [Then("não devo receber uma sessão válida")]
    public async Task Rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, world.Response.StatusCode);
        var body = await ApiScenario.Json(world.Response);
        Assert.Equal("Credenciais invalidas.", body.GetProperty("message").GetString());
        Assert.False(body.TryGetProperty("token", out _));
        using var protectedResponse = await world.Actor.GetAsync("/api/WaterSensors");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.Empty(await protectedResponse.Content.ReadAsStringAsync());
    }
}
