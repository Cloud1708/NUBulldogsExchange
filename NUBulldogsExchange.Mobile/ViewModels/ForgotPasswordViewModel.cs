using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NUBulldogsExchange.Mobile.Helpers;
using NUBulldogsExchange.Web.Shared.Data;
using NUBulldogsExchange.Web.Shared.Services;

namespace NUBulldogsExchange.Mobile.ViewModels;

public sealed class ForgotPasswordViewModel : INotifyPropertyChanged, IDisposable
{
    public enum Step
    {
        EnterEmail,
        SendingCode,
        VerifyCode,
        NewPassword,
        Resetting,
        Success
    }

    private readonly AuthService _auth;
    private readonly ToastService _toast;

    private Step _currentStep = Step.EnterEmail;
    private bool _isBusy;
    private string _email = string.Empty;
    private string? _emailError;
    private string _genericSentMessage = AuthService.PasswordResetGenericMessage;
    private string _otpCode = string.Empty;
    private string? _otpError;
    private bool _codeLocked;
    private string? _resetToken;

    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isNewPasswordHidden = true;
    private bool _isConfirmPasswordHidden = true;
    private string? _passwordError;
    private string? _confirmError;

    private DateTime _codeExpiresAtUtc = DateTime.MinValue;
    private DateTime _resendReadyAtUtc = DateTime.MinValue;
    private System.Threading.Timer? _countdownTimer;

    public ForgotPasswordViewModel(AuthService auth, ToastService toast)
    {
        _auth = auth;
        _toast = toast;

        SendCommand = new Command(async () => await SendCodeAsync(), () => !IsBusy);
        VerifyCommand = new Command(async () => await VerifyCodeAsync(), () => CanVerify);
        ResendCommand = new Command(async () => await ResendCodeAsync(), () => CanResend);
        ResetPasswordCommand = new Command(async () => await ResetPasswordAsync(), () => CanReset);
        ChangeEmailCommand = new Command(OnChangeEmail, () => !IsBusy);
        ToggleNewPasswordCommand = new Command(() => IsNewPasswordHidden = !IsNewPasswordHidden);
        ToggleConfirmPasswordCommand = new Command(() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden);
        BackCommand = new Command(async () => await OnBackAsync());
        BackToLoginCommand = new Command(async () => await GoToLoginAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Step CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStepEnterEmail));
            OnPropertyChanged(nameof(IsStepSendingCode));
            OnPropertyChanged(nameof(IsStepVerifyCode));
            OnPropertyChanged(nameof(IsStepNewPassword));
            OnPropertyChanged(nameof(IsStepResetting));
            OnPropertyChanged(nameof(IsStepSuccess));
            OnPropertyChanged(nameof(ShowStepIndicator));
            OnPropertyChanged(nameof(VisualStep));
            RefreshStepper();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            (SendCommand as Command)?.ChangeCanExecute();
            (VerifyCommand as Command)?.ChangeCanExecute();
            (ResendCommand as Command)?.ChangeCanExecute();
            (ResetPasswordCommand as Command)?.ChangeCanExecute();
            (ChangeEmailCommand as Command)?.ChangeCanExecute();
        }
    }

    // --- Visibility Properties ---
    public bool IsStepEnterEmail => CurrentStep == Step.EnterEmail;
    public bool IsStepSendingCode => CurrentStep == Step.SendingCode;
    public bool IsStepVerifyCode => CurrentStep == Step.VerifyCode;
    public bool IsStepNewPassword => CurrentStep is Step.NewPassword or Step.Resetting;
    public bool IsStepResetting => CurrentStep == Step.Resetting;
    public bool IsStepSuccess => CurrentStep == Step.Success;
    public bool ShowStepIndicator => CurrentStep is not Step.SendingCode and not Step.Success;

    public int VisualStep => CurrentStep switch
    {
        Step.EnterEmail or Step.SendingCode => 1,
        Step.VerifyCode => 2,
        _ => 3
    };

    // --- Stepper Styling ---
    public string Step1Marker => VisualStep > 1 ? "✓" : "1";
    public string Step2Marker => VisualStep > 2 ? "✓" : "2";
    public string Step3Marker => "3";

    public Color Step1Bg => VisualStep >= 1 ? Color.FromArgb("#123A63") : Color.FromArgb("#E2E8F0");
    public Color Step1TextColor => VisualStep >= 1 ? Colors.White : Color.FromArgb("#64748B");
    public Color Step1LabelColor => VisualStep >= 1 ? Color.FromArgb("#123A63") : Color.FromArgb("#64748B");

    public Color Step2Bg => VisualStep >= 2 ? Color.FromArgb("#123A63") : Color.FromArgb("#E2E8F0");
    public Color Step2TextColor => VisualStep >= 2 ? Colors.White : Color.FromArgb("#64748B");
    public Color Step2LabelColor => VisualStep >= 2 ? Color.FromArgb("#123A63") : Color.FromArgb("#64748B");

    public Color Step3Bg => VisualStep >= 3 ? Color.FromArgb("#123A63") : Color.FromArgb("#E2E8F0");
    public Color Step3TextColor => VisualStep >= 3 ? Colors.White : Color.FromArgb("#64748B");
    public Color Step3LabelColor => VisualStep >= 3 ? Color.FromArgb("#123A63") : Color.FromArgb("#64748B");

    private void RefreshStepper()
    {
        OnPropertyChanged(nameof(Step1Marker));
        OnPropertyChanged(nameof(Step2Marker));
        OnPropertyChanged(nameof(Step3Marker));
        OnPropertyChanged(nameof(Step1Bg));
        OnPropertyChanged(nameof(Step1TextColor));
        OnPropertyChanged(nameof(Step1LabelColor));
        OnPropertyChanged(nameof(Step2Bg));
        OnPropertyChanged(nameof(Step2TextColor));
        OnPropertyChanged(nameof(Step2LabelColor));
        OnPropertyChanged(nameof(Step3Bg));
        OnPropertyChanged(nameof(Step3TextColor));
        OnPropertyChanged(nameof(Step3LabelColor));
    }

    // --- Step 1: Email ---
    public string Email
    {
        get => _email;
        set
        {
            if (_email == value) return;
            _email = value;
            EmailError = null;
            OnPropertyChanged();
        }
    }

    public string? EmailError
    {
        get => _emailError;
        set
        {
            if (_emailError == value) return;
            _emailError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEmailError));
        }
    }

    public bool HasEmailError => !string.IsNullOrWhiteSpace(EmailError);

    // --- Step 2: Verification Code ---
    public string MaskedEmail => AuthValidation.MaskEmail(Email);
    public string GenericSentMessage => _genericSentMessage;

    public string OtpCode
    {
        get => _otpCode;
        set
        {
            var clean = AuthValidation.DigitsOnly(value);
            if (clean.Length > 6)
                clean = clean[..6];

            if (_otpCode == clean) return;
            _otpCode = clean;
            OtpError = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Digit0));
            OnPropertyChanged(nameof(Digit1));
            OnPropertyChanged(nameof(Digit2));
            OnPropertyChanged(nameof(Digit3));
            OnPropertyChanged(nameof(Digit4));
            OnPropertyChanged(nameof(Digit5));
            OnPropertyChanged(nameof(CanVerify));
            (VerifyCommand as Command)?.ChangeCanExecute();

            if (_otpCode.Length == 6 && CanVerify)
            {
                MainThread.BeginInvokeOnMainThread(async () => await VerifyCodeAsync());
            }
        }
    }

    public string Digit0 => GetDigit(0);
    public string Digit1 => GetDigit(1);
    public string Digit2 => GetDigit(2);
    public string Digit3 => GetDigit(3);
    public string Digit4 => GetDigit(4);
    public string Digit5 => GetDigit(5);

    private string GetDigit(int index) =>
        _otpCode.Length > index ? _otpCode[index].ToString() : string.Empty;

    public string? OtpError
    {
        get => _otpError;
        set
        {
            if (_otpError == value) return;
            _otpError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOtpError));
        }
    }

    public bool HasOtpError => !string.IsNullOrWhiteSpace(OtpError);

    private TimeSpan CodeRemaining =>
        _codeExpiresAtUtc == DateTime.MinValue ? TimeSpan.Zero : _codeExpiresAtUtc - DateTime.UtcNow;

    private TimeSpan ResendRemaining =>
        _resendReadyAtUtc == DateTime.MinValue ? TimeSpan.Zero : _resendReadyAtUtc - DateTime.UtcNow;

    public bool IsCodeExpired => CurrentStep == Step.VerifyCode && CodeRemaining <= TimeSpan.Zero;

    public string CodeRemainingText
    {
        get
        {
            if (_codeExpiresAtUtc == DateTime.MinValue) return string.Empty;
            if (IsCodeExpired) return "Verification code expired.";
            var rem = CodeRemaining;
            return $"Code expires in {rem.Minutes:D2}:{rem.Seconds:D2}";
        }
    }

    public string ResendCooldownText
    {
        get
        {
            var rem = ResendRemaining;
            if (rem <= TimeSpan.Zero) return string.Empty;
            return $"You can resend a new code in {rem.Minutes:D2}:{rem.Seconds:D2}";
        }
    }

    public bool CanResend => !IsBusy && ResendRemaining <= TimeSpan.Zero;

    public bool CanVerify =>
        !IsBusy && !_codeLocked && !IsCodeExpired && _otpCode.Length == 6;

    // --- Step 3: New Password ---
    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (_newPassword == value) return;
            _newPassword = value;
            PasswordError = null;
            OnPropertyChanged();
            RefreshPasswordValidation();
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (_confirmPassword == value) return;
            _confirmPassword = value;
            ConfirmError = null;
            OnPropertyChanged();
            RefreshPasswordValidation();
        }
    }

    public bool IsNewPasswordHidden
    {
        get => _isNewPasswordHidden;
        set
        {
            if (_isNewPasswordHidden == value) return;
            _isNewPasswordHidden = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NewPasswordToggleGlyph));
        }
    }

    public bool IsConfirmPasswordHidden
    {
        get => _isConfirmPasswordHidden;
        set
        {
            if (_isConfirmPasswordHidden == value) return;
            _isConfirmPasswordHidden = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConfirmPasswordToggleGlyph));
        }
    }

    public string NewPasswordToggleGlyph =>
        IsNewPasswordHidden ? MaterialIconCodes.VisibilityOff : MaterialIconCodes.Visibility;

    public string ConfirmPasswordToggleGlyph =>
        IsConfirmPasswordHidden ? MaterialIconCodes.VisibilityOff : MaterialIconCodes.Visibility;

    public string? PasswordError
    {
        get => _passwordError;
        set
        {
            if (_passwordError == value) return;
            _passwordError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPasswordError));
        }
    }

    public bool HasPasswordError => !string.IsNullOrWhiteSpace(PasswordError);

    public string? ConfirmError
    {
        get => _confirmError;
        set
        {
            if (_confirmError == value) return;
            _confirmError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasConfirmError));
        }
    }

    public bool HasConfirmError => !string.IsNullOrWhiteSpace(ConfirmError);

    public bool IsLengthMet => AuthValidation.MeetsPasswordPolicy(NewPassword);
    public bool IsCaseMet => AuthValidation.HasUpperAndLower(NewPassword);
    public bool IsDigitMet => AuthValidation.HasDigit(NewPassword);
    public bool IsSpecialMet => AuthValidation.HasSpecial(NewPassword);
    public bool IsMatchMet =>
        !string.IsNullOrEmpty(ConfirmPassword) &&
        string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal);

    public Color LengthColor => IsLengthMet ? Color.FromArgb("#16A34A") : Color.FromArgb("#94A3B8");
    public Color CaseColor => IsCaseMet ? Color.FromArgb("#16A34A") : Color.FromArgb("#94A3B8");
    public Color DigitColor => IsDigitMet ? Color.FromArgb("#16A34A") : Color.FromArgb("#94A3B8");
    public Color SpecialColor => IsSpecialMet ? Color.FromArgb("#16A34A") : Color.FromArgb("#94A3B8");
    public Color MatchColor => IsMatchMet ? Color.FromArgb("#16A34A") : Color.FromArgb("#94A3B8");

    public string LengthGlyph => IsLengthMet ? MaterialIconCodes.Check : "•";
    public string CaseGlyph => IsCaseMet ? MaterialIconCodes.Check : "•";
    public string DigitGlyph => IsDigitMet ? MaterialIconCodes.Check : "•";
    public string SpecialGlyph => IsSpecialMet ? MaterialIconCodes.Check : "•";
    public string MatchGlyph => IsMatchMet ? MaterialIconCodes.Check : "•";

    public bool CanReset =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(_resetToken) &&
        IsLengthMet &&
        IsCaseMet &&
        IsDigitMet &&
        IsSpecialMet &&
        IsMatchMet;

    private void RefreshPasswordValidation()
    {
        OnPropertyChanged(nameof(IsLengthMet));
        OnPropertyChanged(nameof(IsCaseMet));
        OnPropertyChanged(nameof(IsDigitMet));
        OnPropertyChanged(nameof(IsSpecialMet));
        OnPropertyChanged(nameof(IsMatchMet));
        OnPropertyChanged(nameof(LengthColor));
        OnPropertyChanged(nameof(CaseColor));
        OnPropertyChanged(nameof(DigitColor));
        OnPropertyChanged(nameof(SpecialColor));
        OnPropertyChanged(nameof(MatchColor));
        OnPropertyChanged(nameof(LengthGlyph));
        OnPropertyChanged(nameof(CaseGlyph));
        OnPropertyChanged(nameof(DigitGlyph));
        OnPropertyChanged(nameof(SpecialGlyph));
        OnPropertyChanged(nameof(MatchGlyph));
        OnPropertyChanged(nameof(CanReset));
        (ResetPasswordCommand as Command)?.ChangeCanExecute();
    }

    // --- Commands ---
    public ICommand SendCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand ResendCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand ChangeEmailCommand { get; }
    public ICommand ToggleNewPasswordCommand { get; }
    public ICommand ToggleConfirmPasswordCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand BackToLoginCommand { get; }

    private async Task SendCodeAsync()
    {
        if (IsBusy) return;
        Email = Email.Trim();
        if (!AuthValidation.IsValidEmail(Email))
        {
            EmailError = "Enter a valid email address.";
            return;
        }

        IsBusy = true;
        CurrentStep = Step.SendingCode;
        try
        {
            var result = await _auth.SendPasswordResetCodeAsync(Email);
            if (!result.Success)
            {
                CurrentStep = Step.EnterEmail;
                EmailError = result.Error ?? "Unable to send a verification code. Please try again.";
                _toast.Show(EmailError);
                return;
            }

            _genericSentMessage = result.Message;
            OnPropertyChanged(nameof(GenericSentMessage));
            OnPropertyChanged(nameof(MaskedEmail));

            BeginTimers(result.ExpiresInSeconds, result.ResendCooldownSeconds);
            OtpCode = string.Empty;
            OtpError = null;
            _codeLocked = false;
            _resetToken = null;
            CurrentStep = Step.VerifyCode;
        }
        catch (Exception ex)
        {
            CurrentStep = Step.EnterEmail;
            EmailError = "Unable to process request: " + ex.Message;
            _toast.Show(EmailError);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResendCodeAsync()
    {
        if (IsBusy || !CanResend) return;
        IsBusy = true;
        try
        {
            var result = await _auth.SendPasswordResetCodeAsync(Email);
            if (!result.Success)
            {
                OtpError = result.Error ?? "Unable to resend verification code. Please try again.";
                _toast.Show(OtpError);
                return;
            }

            _genericSentMessage = result.Message;
            OnPropertyChanged(nameof(GenericSentMessage));
            BeginTimers(result.ExpiresInSeconds, result.ResendCooldownSeconds);
            OtpCode = string.Empty;
            OtpError = null;
            _codeLocked = false;
            _resetToken = null;
            _toast.Show(_genericSentMessage);
        }
        catch (Exception ex)
        {
            OtpError = "Unable to resend: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task VerifyCodeAsync()
    {
        if (!CanVerify) return;
        if (IsCodeExpired)
        {
            OtpError = "Verification code expired.";
            _codeLocked = true;
            return;
        }

        IsBusy = true;
        OtpError = null;
        try
        {
            var result = await _auth.VerifyPasswordResetCodeAsync(Email, OtpCode);
            if (!result.Success || string.IsNullOrWhiteSpace(result.ResetToken))
            {
                OtpError = result.Error ?? "Invalid verification code. Please try again.";
                if (result.RequireNewCode)
                    _codeLocked = true;
                return;
            }

            _resetToken = result.ResetToken;
            StopTimer();
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            PasswordError = null;
            ConfirmError = null;
            CurrentStep = Step.NewPassword;
        }
        catch (Exception ex)
        {
            OtpError = "Verification failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetPasswordAsync()
    {
        if (IsBusy) return;
        PasswordError = null;
        ConfirmError = null;

        if (string.IsNullOrWhiteSpace(_resetToken))
        {
            CurrentStep = Step.EnterEmail;
            EmailError = "This reset session is no longer valid. Please request a new code.";
            return;
        }

        if (!IsLengthMet)
        {
            PasswordError = $"Password must be at least {AuthValidation.MinPasswordLength} characters.";
            return;
        }

        if (!IsMatchMet)
        {
            ConfirmError = "Passwords do not match.";
            return;
        }

        IsBusy = true;
        CurrentStep = Step.Resetting;
        try
        {
            var result = await _auth.ResetPasswordWithTokenAsync(
                Email,
                _resetToken,
                NewPassword,
                ConfirmPassword);

            if (!result.Success)
            {
                CurrentStep = Step.NewPassword;
                PasswordError = result.Error ?? "Unable to reset password. Please try again.";
                if (PasswordError.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                    PasswordError.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
                    PasswordError.Contains("new code", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentStep = Step.EnterEmail;
                    EmailError = PasswordError;
                }
                return;
            }

            _resetToken = null;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            CurrentStep = Step.Success;
        }
        catch (Exception ex)
        {
            CurrentStep = Step.NewPassword;
            PasswordError = "Reset failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnChangeEmail()
    {
        StopTimer();
        OtpCode = string.Empty;
        OtpError = null;
        CurrentStep = Step.EnterEmail;
    }

    private async Task OnBackAsync()
    {
        if (CurrentStep == Step.VerifyCode)
        {
            OnChangeEmail();
            return;
        }

        if (CurrentStep is Step.NewPassword or Step.Resetting)
        {
            OnChangeEmail();
            return;
        }

        await GoToLoginAsync();
    }

    private static async Task GoToLoginAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void BeginTimers(int codeExpiresSeconds, int resendCooldownSeconds)
    {
        var now = DateTime.UtcNow;
        _codeExpiresAtUtc = now.AddSeconds(codeExpiresSeconds > 0 ? codeExpiresSeconds : 600);
        _resendReadyAtUtc = now.AddSeconds(resendCooldownSeconds > 0 ? resendCooldownSeconds : 60);

        StopTimer();
        _countdownTimer = new System.Threading.Timer(OnTimerTick, null, 1000, 1000);
        UpdateTimerProperties();
    }

    private void StopTimer()
    {
        _countdownTimer?.Dispose();
        _countdownTimer = null;
    }

    private void OnTimerTick(object? state)
    {
        MainThread.BeginInvokeOnMainThread(UpdateTimerProperties);
    }

    private void UpdateTimerProperties()
    {
        OnPropertyChanged(nameof(CodeRemainingText));
        OnPropertyChanged(nameof(ResendCooldownText));
        OnPropertyChanged(nameof(IsCodeExpired));
        OnPropertyChanged(nameof(CanResend));
        OnPropertyChanged(nameof(CanVerify));
        (ResendCommand as Command)?.ChangeCanExecute();
        (VerifyCommand as Command)?.ChangeCanExecute();

        if (CodeRemaining <= TimeSpan.Zero && ResendRemaining <= TimeSpan.Zero)
        {
            StopTimer();
        }
    }

    public void Dispose()
    {
        StopTimer();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
