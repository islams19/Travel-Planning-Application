using TMPro;
using UnityEngine;
using TravelApp;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
public class ProductCardUI : MonoBehaviour
{
    [Header("Product Information")]
    [SerializeField] private UIImage productImage;
    [SerializeField] private TMP_Text nameText, typeText, locationText, priceText, ratingText, dateText, descriptionText;
    [Header("Buttons")] [SerializeField] private UIButton bookButton;
    Product product;
    void Awake() { if (bookButton != null) bookButton.onClick.AddListener(BookProduct); }
    static void Set(TMP_Text target, string text) { if (target != null) target.text = text; }
    public void SetProduct(Product value)
    {
        product = value;
        if (value == null) return;
        if (productImage != null) { productImage.sprite = value.productImage; productImage.enabled = value.productImage != null; }
        Set(nameText, value.productName); Set(typeText, value.productType.ToString()); Set(locationText, value.location);
        Set(priceText, "$" + value.price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        Set(ratingText, value.rating.ToString("0.0") + " / 5"); Set(descriptionText, value.description);
        Set(dateText, value.startDate.ToDateTime().ToString("yyyy-MM-dd") + " – " + value.endDate.ToDateTime().ToString("yyyy-MM-dd"));
        if (bookButton != null) bookButton.interactable = value.available;
    }
    void BookProduct()
    {
        if (product == null) return;
        if (BookingManager.Instance != null) BookingManager.Instance.Select(product);
        else TravelScreenUI.Current?.ShowProduct(product, "", "");
    }
    void OnDestroy() { if (bookButton != null) bookButton.onClick.RemoveListener(BookProduct); }
}
