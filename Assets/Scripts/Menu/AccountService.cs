using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

public static class AccountService
{
    public static string Username { get; private set; }
    public static string PlayerId => AuthenticationService.Instance.PlayerId;
    public static bool SignedIn =>
        UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

    public static async Task SignIn(string user, string pass)
    {
        await Init();
        await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(user, Salted(pass));
        Username = user;
    }

    public static async Task SignUp(string user, string pass)
    {
        await Init();
        await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(user, Salted(pass));
        Username = user;
    }

    public static async Task<System.DateTime?> MemberSince() =>
        (await AuthenticationService.Instance.GetPlayerInfoAsync()).CreatedAt;

    public static void SignOut()
    {
        AuthenticationService.Instance.SignOut(true);
        Username = null;
    }

    public static string Validate(string user, string pass, bool signUp)
    {
        if (user.Length < 3 || user.Length > 20) return "Username must be 3-20 characters.";
        if (!user.All(c => char.IsLetterOrDigit(c) || ".-@_".Contains(c)))
            return "Username can only use letters, numbers and . - @ _";
        if (pass.Length == 0) return "Enter a password.";
        if (!signUp) return null;
        if (pass.Length < 8 || pass.Length > 26) return "Password must be 8-26 characters.";
        return null;
    }

    static string Salted(string pass) => pass + "Aa1!";

    static async Task Init()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();
    }
}
