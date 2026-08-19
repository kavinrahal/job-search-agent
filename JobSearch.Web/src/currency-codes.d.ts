// currency-codes ships no type declarations of its own and none exist on @types.
declare module "currency-codes/data" {
  interface CurrencyRecord {
    code: string;
    number: string;
    digits: number;
    currency: string;
    countries: string[];
  }
  const data: CurrencyRecord[];
  export default data;
}
