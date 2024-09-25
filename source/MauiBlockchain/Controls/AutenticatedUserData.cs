namespace MauiBlockchain
{
    public class AutenticatedUserData
    {
        public string WalletName { get; set; }
        public string UserAddress { get; set; }
        public string UserPublicKey { get; set; }
        public string AuthenticationNonce { get; set; }
    }
}
