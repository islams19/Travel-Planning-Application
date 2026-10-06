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
    public string productName;

    [TextArea]
    public string description;

    public ProductType productType;

    public float price;
}