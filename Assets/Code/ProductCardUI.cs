using TMPro;
using UnityEngine;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;

public class ProductCardUI : MonoBehaviour
{
    [Header("Product Information")]
    [SerializeField] private UIImage productImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text ratingText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Buttons")]
    [SerializeField] private UIButton bookButton;

    private Product product;

    private void Awake()
    {
        if (bookButton != null)
        {
            bookButton.onClick.AddListener(BookProduct);
        }
    }

    public void SetProduct(Product newProduct)
    {
        product = newProduct;

        productImage.sprite = product.productImage;

        nameText.text = product.productName;
        typeText.text = product.productType.ToString();
        locationText.text = product.location;

        priceText.text =
            "$" + product.price.ToString("0.00");

        ratingText.text =
            product.rating.ToString("0.0") + " / 5";

        descriptionText.text =
            product.description;

        dateText.text =
            product.startDate.ToDateTime().ToString("MMM d, yyyy")
            + " - "
            + product.endDate.ToDateTime().ToString("MMM d, yyyy");
    }

    private void BookProduct()
    {
        if (product == null)
        {
            Debug.LogError("No product assigned to this card.");
            return;
        }

        if (LoginManager.Instance == null ||
            !LoginManager.Instance.IsLoggedIn())
        {
            Debug.LogWarning("User must be logged in.");
            return;
        }

        User currentUser =
            LoginManager.Instance.CurrentUser;

        currentUser.cart.Add(product);

        Debug.Log(
            product.productName +
            " added to " +
            currentUser.username +
            "'s cart."
        );
    }

    private void OnDestroy()
    {
        if (bookButton != null)
        {
            bookButton.onClick.RemoveListener(BookProduct);
        }
    }
}