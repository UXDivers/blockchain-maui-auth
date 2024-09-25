namespace MauiBlockchain;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        
        walletConnectButton.SignedInCommand = new Command<AutenticatedUserData>(OnSignedInCommand);
    }

    private void OnSignedInCommand(AutenticatedUserData userData)
    {
        Application.Current.Dispatcher.Dispatch(() =>
        {
            buttonContainter.IsVisible = false;
            dataContainer.IsVisible = true;
            authNonceLabel.Text = userData.AuthenticationNonce;
            userAddressLabel.Text = userData.UserAddress;
            walletNameLabel.Text = userData.WalletName;
            userPublicKeyLabel.Text = userData.UserPublicKey;

            DisplayAlert("Success", "User logged in successfully", "Ok");
        });
    }
}
