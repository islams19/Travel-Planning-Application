using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UserDatabase",
    menuName = "Travel App/User Database"
)]
public class UserDatabase : ScriptableObject
{
    public List<User> users = new List<User>();

    public User GetUser(string username)
    {
        foreach (User user in users)
        {
            if (user.username.Equals(
                username,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return user;
            }
        }

        return null;
    }
}