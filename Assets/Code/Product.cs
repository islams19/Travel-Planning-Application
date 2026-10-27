using UnityEngine;

public enum ProductType
{
    Hotel,
    Flight,
    Restaurant,
    Experience
}

[CreateAssetMenu(
    fileName = "NewProduct",
    menuName = "Travel App/Product"
)]
public class Product : ScriptableObject
{
    [Header("Basic Information")]
    public string productName;

    [TextArea]
    public string description;

    public ProductType productType;

    [Header("Image")]

    public Sprite productImage;

    [Header("Booking Information")]
    public string location;

    public float price;

    [Range(0, 5)]
    public float rating;

    public bool available = true;

    [Header("Availability Dates")]
    public TravelDate startDate;
    public TravelDate endDate;
}