using System.Net.Http;
using System.Threading.Tasks;
using Core.Network.Platform.Entities;

namespace Core.Network.Platform;

public class AuthorizationApi
{
	private static readonly HttpClient HttpClient = new();

	public async Task<string?> Login(LoginDto loginDto) =>
		await HttpClient.SendRequest<string, LoginDto>("http://localhost/auth", loginDto);
}
