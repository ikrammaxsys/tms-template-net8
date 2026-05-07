namespace tms_template_net8.ViewModels;

public class ConfirmationModalViewModel
{
    public string ModalId { get; set; } = "confirmation-modal";
    public string Title { get; set; } = "Confirmation";
    public string Message { get; set; } = "";
    public string ConfirmButtonId { get; set; } = "btnConfirm";
    public bool UseSmallButtons { get; set; } = true;
}
