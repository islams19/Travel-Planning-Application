// Display-only deterministic policy examples. Never changes the booked fare or payment total.
public sealed class ReviewFareRules
{
    public bool Refundable { get; }
    public int ChangeFee { get; }
    public string RefundLabel => Refundable?"Refundable":"Non-refundable";
    public string ChangeLabel => ChangeFee==0?"Changes permitted":"Changes for $"+ChangeFee;
    public string RefundDescription => Refundable?"Refunds are permitted under this demo fare. No real cancellation or refund is processed.":"This demo fare is non-refundable after the illustrative 24-hour cancellation window.";
    public string ChangeDescription => ChangeFee==0?"Changes are permitted without a change fee in this demo policy; any fare difference would apply.":$"Changes are permitted for an illustrative ${ChangeFee} fee per traveler, plus any fare difference. No change fee is charged by this page.";
    ReviewFareRules(bool refundable,int fee) {Refundable=refundable;ChangeFee=fee;}
    public static ReviewFareRules For(string cabin)=>cabin=="Business"||cabin=="First"?new ReviewFareRules(true,0):new ReviewFareRules(false,cabin=="Premium Economy"?35:75);
}
