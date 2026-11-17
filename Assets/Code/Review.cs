using UnityEngine;

[CreateAssetMenu(
    fileName = "NewReview",
    menuName = "Travel App/Review"
)]
public class Review : ScriptableObject
{
    public User author;

    public Product product;

    [Range(1, 5)]
    public int rating;

    [TextArea(3, 10)]
    public string reviewText;
}