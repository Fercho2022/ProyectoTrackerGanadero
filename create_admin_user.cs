using System;
using System.Security.Cryptography;
using System.Text;

// Script para generar hash de contraseña y SQL para crear usuario administrador
class Program
{
    static void Main()
    {
        Console.WriteLine("=== Generador de Usuario Administrador ===\n");

        // Datos del administrador
        string adminEmail = "admin@trackerganadero.com";
        string adminName = "Administrador";
        string adminPassword = "admin123";  // Cámbiala después del primer login

        // Generar hash (mismo método que UsersController.cs)
        string passwordHash = HashPassword(adminPassword);

        Console.WriteLine($"Email: {adminEmail}");
        Console.WriteLine($"Nombre: {adminName}");
        Console.WriteLine($"Contraseña: {adminPassword}");
        Console.WriteLine($"Hash: {passwordHash}\n");

        // Generar SQL
        string sql = $@"
-- Crear usuario administrador
INSERT INTO ""Users"" (""Name"", ""Email"", ""PasswordHash"", ""Role"", ""IsActive"", ""CreatedAt"")
VALUES (
    '{adminName}',
    '{adminEmail}',
    '{passwordHash}',
    'Admin',
    true,
    NOW()
)
ON CONFLICT (""Email"") DO UPDATE SET
    ""Role"" = 'Admin',
    ""IsActive"" = true,
    ""PasswordHash"" = '{passwordHash}';

-- Verificar que se creó
SELECT ""Id"", ""Name"", ""Email"", ""Role"", ""IsActive""
FROM ""Users""
WHERE ""Email"" = '{adminEmail}';
";

        Console.WriteLine("=== SQL para ejecutar en PostgreSQL ===\n");
        Console.WriteLine(sql);

        // Guardar en archivo
        string sqlFilePath = "insert_admin_user.sql";
        System.IO.File.WriteAllText(sqlFilePath, sql);
        Console.WriteLine($"\n✓ SQL guardado en: {sqlFilePath}");
        Console.WriteLine("\nPuedes ejecutarlo con:");
        Console.WriteLine($"  psql -U tu_usuario -d cattletracking -f {sqlFilePath}");
    }

    static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "your-salt-here"));
        return Convert.ToBase64String(hashedBytes);
    }
}
