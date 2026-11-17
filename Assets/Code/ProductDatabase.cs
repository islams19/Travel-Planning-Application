using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ProductDatabase",
    menuName = "Travel App/Product Database"
)]
public class ProductDatabase : ScriptableObject
{
    public List<Product> products = new List<Product>();
}