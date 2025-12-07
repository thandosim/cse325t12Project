using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace t12Project.Migrations
{
    /// <inheritdoc />
    public partial class SeedCapeTownLoads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete all existing bookings first (foreign key constraint)
            migrationBuilder.Sql("DELETE FROM \"Bookings\";");
            
            // Delete all existing loads
            migrationBuilder.Sql("DELETE FROM \"Loads\";");
            
            // Get a customer ID to assign loads to (we'll use the first customer)
            // Note: In production, you'd want to handle this more carefully
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_customer_id uuid;
                BEGIN
                    -- Get first customer
                    SELECT ""Id"" INTO v_customer_id 
                    FROM ""AspNetUsers"" 
                    WHERE ""AccountType"" = 1 
                    LIMIT 1;
                    
                    -- Insert Cape Town loads
                    INSERT INTO ""Loads"" (""Id"", ""Title"", ""Status"", ""PickupLocation"", ""DropoffLocation"", 
                                         ""PickupLatitude"", ""PickupLongitude"", ""DropoffLatitude"", ""DropoffLongitude"",
                                         ""PickupDate"", ""WeightLbs"", ""Description"", ""CargoType"", ""CustomerId"", ""CreatedAt"")
                    VALUES
                        (gen_random_uuid(), 'Office Furniture Relocation', 'Available', 'Pinelands, Cape Town', 'Bellville Industrial, Cape Town',
                         -33.9349, 18.4956, -33.9139, 18.6289, CURRENT_TIMESTAMP + INTERVAL '1 day', 2500,
                         'Complete office furniture set - desks, chairs, filing cabinets. Handle with care.', 'General', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Medical Equipment', 'Available', 'Ndabeni, Cape Town', 'Panorama Mediclinic, Cape Town',
                         -33.9356, 18.5112, -33.8694, 18.5686, CURRENT_TIMESTAMP + INTERVAL '6 hours', 1200,
                         'Fragile medical diagnostic equipment. Temperature controlled transport preferred.', 'Fragile', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Restaurant Supplies', 'Available', 'Maitland, Cape Town', 'Claremont, Cape Town',
                         -33.9442, 18.5156, -33.9789, 18.4644, CURRENT_TIMESTAMP + INTERVAL '2 days', 3500,
                         'Industrial kitchen equipment and frozen food supplies. Refrigerated transport required.', 'Refrigerated', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Building Materials', 'Available', 'Goodwood, Cape Town', 'Thornton, Cape Town',
                         -33.9167, 18.5500, -33.8956, 18.5167, CURRENT_TIMESTAMP + INTERVAL '1 day', 8000,
                         'Cement bags, bricks, and steel reinforcement bars. Flatbed truck required.', 'Flatbed', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Electronics Shipment', 'Available', 'Salt River, Cape Town', 'Tyger Valley, Cape Town',
                         -33.9344, 18.4511, -33.8656, 18.6456, CURRENT_TIMESTAMP + INTERVAL '3 days', 1800,
                         'Consumer electronics - TVs, computers, tablets. Extremely fragile, insurance required.', 'Fragile', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Fresh Produce Delivery', 'Available', 'Epping Market, Cape Town', 'Gardens Centre, Cape Town',
                         -33.9167, 18.5333, -33.9356, 18.4108, CURRENT_TIMESTAMP + INTERVAL '12 hours', 2200,
                         'Fresh fruits and vegetables for restaurant. Early morning delivery preferred.', 'Refrigerated', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Gym Equipment', 'Available', 'Rondebosch, Cape Town', 'Pinelands, Cape Town',
                         -33.9633, 18.4789, -33.9349, 18.4956, CURRENT_TIMESTAMP + INTERVAL '2 days', 5500,
                         'Complete home gym setup - weights, machines, exercise bikes.', 'General', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Pharmaceuticals', 'Available', 'Brooklyn, Cape Town', 'N1 City, Cape Town',
                         -33.9289, 18.4878, -33.9120, 18.5350, CURRENT_TIMESTAMP + INTERVAL '1 day', 900,
                         'Temperature-sensitive pharmaceutical products. Refrigerated transport mandatory.', 'Refrigerated', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Auto Parts', 'Available', 'Parow, Cape Town', 'Milnerton, Cape Town',
                         -33.9047, 18.5814, -33.8706, 18.4978, CURRENT_TIMESTAMP + INTERVAL '3 days', 3200,
                         'Automotive parts and accessories for workshop. Contains heavy engine components.', 'General', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Wine & Spirits', 'Available', 'Observatory, Cape Town', 'Sea Point, Cape Town',
                         -33.9364, 18.4726, -33.9253, 18.3892, CURRENT_TIMESTAMP + INTERVAL '2 days', 1600,
                         'Premium wine collection and spirits. Fragile glass bottles, handle with extreme care.', 'Fragile', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Textiles', 'Available', 'Woodstock, Cape Town', 'Durbanville, Cape Town',
                         -33.9258, 18.4473, -33.8300, 18.6489, CURRENT_TIMESTAMP + INTERVAL '4 days', 2800,
                         'Fabric rolls and textile products for manufacturing. Keep dry.', 'General', v_customer_id, CURRENT_TIMESTAMP),
                        
                        (gen_random_uuid(), 'Frozen Seafood', 'Available', 'Table Bay Harbour, Cape Town', 'Brackenfell, Cape Town',
                         -33.9089, 18.4289, -33.8645, 18.6823, CURRENT_TIMESTAMP + INTERVAL '8 hours', 4500,
                         'Fresh frozen seafood from harbor. Requires refrigerated transport at -18°C.', 'Refrigerated', v_customer_id, CURRENT_TIMESTAMP);
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the Cape Town loads
            migrationBuilder.Sql(@"
                DELETE FROM ""Loads"" 
                WHERE ""PickupLocation"" LIKE '%Cape Town%';
            ");
        }
    }
}
