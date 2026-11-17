using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewUser",
    menuName = "Travel App/User"
)]
public class User : ScriptableObject
{
    [Header("Account Information")]
    public string username;
    public string email;
    public string password;

    [Header("Permissions")]
    public UserRole role;

    [Header("User Data")]
    public List<Product> cart = new List<Product>();
    public List<Product> purchaseHistory = new List<Product>();
    public List<Review> reviewsSubmitted = new List<Review>();
}