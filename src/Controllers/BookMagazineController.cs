using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims; // Utilisé pour manipuler les informations des utilisateurs (claims) dans les tokens d'authentification, comme l'identifiant de l'utilisateur (UserId).


[Route("api/[controller]")]
[ApiController]
public class BookMagazineController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BookMagazineController(ApplicationDbContext context)
    {
        _context = context;
    }

    // *** Ajouter un livre ou magazine avec un auteur et une catégorie ***
    [HttpPost("add")]
    [Authorize]
    public async Task<IActionResult> AddBookMagazine([FromForm] BookMagazineModel model)
    {
        // Vérifier si l'auteur existe, sinon le créer
        var author = _context.Authors.FirstOrDefault(a => a.Name == model.Author);
        if (author == null)
        {
            author = new Author { Name = model.Author };
            _context.Authors.Add(author);
            await _context.SaveChangesAsync();
        }

        // Vérifier si la catégorie existe, sinon la créer
        var category = _context.Categories.FirstOrDefault(c => c.Name == model.Category);
        if (category == null)
        {
            category = new Category { Name = model.Category };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
        }

        // Enregistrement du fichier du livre/magazine
        // var filePath = Path.Combine("wwwroot/files", model.File.FileName);
        // Générer un nom de fichier unique (UUID)
        //var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.File.FileName)}";  // Conserver l'extension originale
        // Générer un nom de fichier unique (UUID)
        string uniqueFileName;
        do
        {
            uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.File.FileName)}";
        }
        while (_context.FileUuids.Any(f => f.Uuid == uniqueFileName));  // Vérification de l'unicité

        // Sauvegarder l'UUID dans la table FileUuids
        var fileUuid = new FileUuid { Uuid = uniqueFileName };
        _context.FileUuids.Add(fileUuid);
        await _context.SaveChangesAsync();
        
        var filePath = Path.Combine("wwwroot/files", uniqueFileName); // Enregistrement du fichier du livre/magazine
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await model.File.CopyToAsync(stream);
        }

        // Enregistrement de l'image de couverture si elle est présente
        // string coverImagePath = null;
        // if (model.CoverImage != null && model.CoverImage.Length > 0)
        // {
        //     coverImagePath = Path.Combine("wwwroot/images/covers", model.CoverImage.FileName);
        //     using (var coverStream = new FileStream(coverImagePath, FileMode.Create))
        //     {
        //         await model.CoverImage.CopyToAsync(coverStream);
        //     }
        //     coverImagePath = $"/images/covers/{model.CoverImage.FileName}";
        // }

        // Enregistrement de l'image de couverture si elle est présente
        string coverImagePath = null;
        string originalCoverImageName = null;
        
        if (model.CoverImage != null && model.CoverImage.Length > 0)
        {
            originalCoverImageName = model.CoverImage.FileName; // Stocker le nom original de l'image de couverture

            // Générer un UUID unique pour l'image de couverture
            string uuid;
            do
            {
                uuid = Guid.NewGuid().ToString();
            }
            while (_context.CoverImageUuids.Any(u => u.Uuid == uuid));  // Vérifier si ce UUID existe déjà

            // Enregistrer l'UUID dans la table pour garantir l'unicité
            _context.CoverImageUuids.Add(new CoverImageUuid { Uuid = uuid });
            await _context.SaveChangesAsync();

            var coverImageExtension = Path.GetExtension(model.CoverImage.FileName);
            var coverImageFileName = uuid + coverImageExtension;
            coverImagePath = Path.Combine("wwwroot/images/covers", coverImageFileName);

            // Sauvegarder l'image de couverture avec le nom UUID
            using (var coverStream = new FileStream(coverImagePath, FileMode.Create))
            {
                await model.CoverImage.CopyToAsync(coverStream);
            }

            // Stocker le chemin relatif dans la base de données
            coverImagePath = $"/images/covers/{coverImageFileName}";
        }

        // Création de l'objet BookMagazine
        var bookMagazine = new BookMagazine
        {
            Title = model.Title,
            AuthorId = author.Id,  // Association avec l'auteur
            CategoryId = category.Id,  // Association avec la catégorie
            Description = model.Description,
            Tags = model.Tags,
            // FilePath = $"/files/{model.File.FileName}",
            FilePath = $"/files/{uniqueFileName}",  // Chemin du fichier avec UUID
            CoverImagePath = coverImagePath,
            OriginalFileName = model.File.FileName,  // Stocker le nom de fichier original
            OriginalCoverImageName = originalCoverImageName  // Nom original de l'image
    
        };

        // Enregistrement dans la base de données
        _context.BooksMagazines.Add(bookMagazine);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Book or magazine added successfully!", CoverImageUrl = coverImagePath });
    }

    // *** Obtenir la liste des livres ou magazines ***
    [HttpGet("list")]
    public IActionResult GetBooksMagazines()
    {
        var booksMagazines = _context.BooksMagazines
            .Select(b => new 
            {
                b.Id,
                b.Title,
                Author = b.Author.Name,
                Category = b.Category.Name,
                b.CoverImagePath,
                b.UploadDate,
                b.ViewCount 
            })
            .ToList();

        return Ok(booksMagazines);
    }

    //*** Obtenir les détails d'un livre ou magazine spécifique ***
    // [HttpGet("{id}")]
    // public IActionResult GetBookMagazine(int id)
    // {
    //     var bookMagazine = _context.BooksMagazines
    //         .Where(b => b.Id == id)
    //         .Select(b => new 
    //         {
    //             b.Id,
    //             b.Title,
    //             b.Description,
    //             Author = b.Author.Name,
    //             Category = b.Category.Name,
    //             b.Tags,
    //             b.CoverImagePath,
    //             b.FilePath,
    //             b.UploadDate
    //         })
    //         .FirstOrDefault();

    //     if (bookMagazine == null)
    //         return NotFound();

    //     return Ok(bookMagazine);        
    // }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBookMagazine(int id)
    {
        var bookMagazine = await _context.BooksMagazines
                .Include(b => b.Author)       // Inclure l'entité 'Author'
                .Include(b => b.Category)     // Inclure l'entité 'Category'
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bookMagazine == null)
            return NotFound();

        // Vérifier que l'entité 'Author' et 'Category' ne sont pas nulles
        if (bookMagazine.Author == null || bookMagazine.Category == null)
            return StatusCode(500, "Invalid data: Author or Category not found.");  // Gérer les cas de données incorrectes


        // Incrémenter le compteur de vues
        bookMagazine.ViewCount++;
        _context.BooksMagazines.Update(bookMagazine);
        await _context.SaveChangesAsync();

        return Ok(new {
            bookMagazine.Id,
            bookMagazine.Title,
            bookMagazine.Description,
            Author = bookMagazine.Author.Name,
            Category = bookMagazine.Category.Name,
            bookMagazine.Tags,
            bookMagazine.CoverImagePath,
            bookMagazine.FilePath,
            bookMagazine.UploadDate,
            bookMagazine.ViewCount // Renvoyer le nombre de vues
        });
    }


    // // *** Télécharger le fichier d'un livre ou magazine ***
    // [HttpGet("download/{id}")]
    // public IActionResult DownloadBookMagazine(int id)
    // {
    //     var bookMagazine = _context.BooksMagazines.FirstOrDefault(b => b.Id == id);
    //     if (bookMagazine == null) return NotFound();

    //     var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", bookMagazine.FilePath.TrimStart('/'));
    //     if (!System.IO.File.Exists(filePath))
    //         return NotFound("File not found on server.");

    //     var fileBytes = System.IO.File.ReadAllBytes(filePath);
    //     var fileName = Path.GetFileName(filePath);

    //     return File(fileBytes, "application/octet-stream", fileName);
    // }

    [HttpGet("download/{id}")]
    public async Task<IActionResult> DownloadBookMagazine(int id)
    {
        //var bookMagazine =  _context.BooksMagazines.FirstOrDefault(b => b.Id == id);
        var bookMagazine = await _context.BooksMagazines
        .FirstOrDefaultAsync(b => b.Id == id);

        if (bookMagazine == null) 
            return NotFound();

        // Incrémenter le compteur de téléchargements
        bookMagazine.DownloadCount++;
        _context.BooksMagazines.Update(bookMagazine);
        await _context.SaveChangesAsync();

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", bookMagazine.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found on server.");

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        //var fileName = Path.GetFileName(filePath);
        var originalFileName = bookMagazine.OriginalFileName;

        return File(fileBytes, "application/octet-stream", originalFileName);
    }


    // *** Mettre à jour un livre ou magazine par l'administrateur ***
    [HttpPut("update/{id}")]
    [Authorize(Roles = "Admin")]  // Seuls les administrateurs peuvent modifier
    public async Task<IActionResult> UpdateBookMagazine(int id, [FromForm] BookMagazineModel model)
    {
        var bookMagazine = _context.BooksMagazines.FirstOrDefault(b => b.Id == id);
        if (bookMagazine == null) return NotFound();

        // Mise à jour des propriétés du livre/magazine
        bookMagazine.Title = model.Title;
        bookMagazine.Description = model.Description;
        bookMagazine.Tags = model.Tags;

        // Gestion de l'auteur et de la catégorie
        var author = _context.Authors.FirstOrDefault(a => a.Name == model.Author);
        if (author == null)
        {
            author = new Author { Name = model.Author };
            _context.Authors.Add(author);
            await _context.SaveChangesAsync();
        }
        bookMagazine.AuthorId = author.Id;

        var category = _context.Categories.FirstOrDefault(c => c.Name == model.Category);
        if (category == null)
        {
            category = new Category { Name = model.Category };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
        }
        bookMagazine.CategoryId = category.Id;

        // Gestion du fichier (facultatif)
        // if (model.File != null)
        // {
        //     var filePath = Path.Combine("wwwroot/files", model.File.FileName);
        //     using (var stream = new FileStream(filePath, FileMode.Create))
        //     {
        //         await model.File.CopyToAsync(stream);
        //     }
        //     bookMagazine.FilePath = $"/files/{model.File.FileName}";
        // }

        if (model.File != null)
        {
            // Supprimer l'ancien fichier du serveur
            if (!string.IsNullOrEmpty(bookMagazine.FilePath))
            {
                var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", bookMagazine.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            // Générer un nouveau nom de fichier UUID
            string uniqueFileName;
            do
            {
                uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(model.File.FileName)}";
            }
            while (_context.FileUuids.Any(f => f.Uuid == uniqueFileName));

            // Sauvegarder le nouvel UUID dans la table FileUuids
            var fileUuid = new FileUuid { Uuid = uniqueFileName };
            _context.FileUuids.Add(fileUuid);
            await _context.SaveChangesAsync();

            // Enregistrer le fichier sur le serveur avec l'UUID
            var filePath = Path.Combine("wwwroot/files", uniqueFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream);
            }

            // Mise à jour du chemin du fichier et du nom de fichier original
            bookMagazine.FilePath = $"/files/{uniqueFileName}";
            bookMagazine.OriginalFileName = model.File.FileName;
        }



        // Gestion de l'image de couverture (facultatif)
        if (model.CoverImage != null)
        {
            var coverImagePath = Path.Combine("wwwroot/images/covers", model.CoverImage.FileName);
            using (var coverStream = new FileStream(coverImagePath, FileMode.Create))
            {
                await model.CoverImage.CopyToAsync(coverStream);
            }
            bookMagazine.CoverImagePath = $"/images/covers/{model.CoverImage.FileName}";
        }

        _context.BooksMagazines.Update(bookMagazine);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Book or magazine updated successfully!" });
    }

    // *** Supprimer un livre ou magazine par l'administrateur ***
    [HttpDelete("delete/{id}")]
    [Authorize(Roles = "Admin")]  // Seuls les administrateurs peuvent supprimer
    public async Task<IActionResult> DeleteBookMagazine(int id)
    {
        var bookMagazine = _context.BooksMagazines.FirstOrDefault(b => b.Id == id);
        if (bookMagazine == null) return NotFound();

        // Suppression des fichiers associés (livre/magazine et image de couverture)
        if (!string.IsNullOrEmpty(bookMagazine.FilePath))
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", bookMagazine.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            // Supprimer l'UUID de la table FileUuids
            var fileUuid = _context.FileUuids.FirstOrDefault(f => f.Uuid == bookMagazine.FilePath.Replace("/files/", ""));
            if (fileUuid != null)
            {
                _context.FileUuids.Remove(fileUuid);
            }
        }

        if (!string.IsNullOrEmpty(bookMagazine.CoverImagePath))
        {
            var coverImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", bookMagazine.CoverImagePath.TrimStart('/'));
            if (System.IO.File.Exists(coverImagePath))
            {
                System.IO.File.Delete(coverImagePath);
            }
        }

        // Suppression du livre/magazine dans la base de données
        _context.BooksMagazines.Remove(bookMagazine);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Book or magazine deleted successfully!" });
    }

    // etape 4
    [HttpGet("search")]
    public IActionResult SearchBooksMagazines([FromQuery] string keyword)
    {
        var booksMagazines = _context.BooksMagazines
            .Where(b => b.Title.Contains(keyword) || 
                        b.Description.Contains(keyword) || 
                        b.Author.Name.Contains(keyword) || 
                        b.Tags.Contains(keyword))
            .Select(b => new {
                b.Id,
                b.Title,
                Author = b.Author.Name,
                b.CoverImagePath,
                b.UploadDate,
                b.ViewCount
            })
            .ToList();
        
        return Ok(booksMagazines);
    }

    [HttpGet("advanced-search")]
    public IActionResult SearchBooksMagazines([FromQuery] string keyword, [FromQuery] string category, [FromQuery] string author, [FromQuery] DateTime? publishDate, [FromQuery] bool sortByPopularity = false)
    {
        var query = _context.BooksMagazines.AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(b => b.Title.Contains(keyword) || 
                                    b.Description.Contains(keyword) || 
                                    b.Tags.Contains(keyword));
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(b => b.Category.Name == category);
        }

        if (!string.IsNullOrEmpty(author))
        {
            query = query.Where(b => b.Author.Name == author);
        }

        if (publishDate.HasValue)
        {
            query = query.Where(b => b.UploadDate >= publishDate.Value);
        }

         // Trier par popularité (ViewCount) si demandé
        if (sortByPopularity)
        {
            query = query.OrderByDescending(b => b.ViewCount);
        }

        var results = query.Select(b => new {
            b.Id,
            b.Title,
            Author = b.Author.Name,
            b.CoverImagePath,
            b.UploadDate,
            b.ViewCount
        }).ToList();

        return Ok(results);
    }

  
    [HttpGet("search/popular")]
    public IActionResult SearchBooksMagazinesByPopularity()
    {
        var booksMagazines = _context.BooksMagazines
            .OrderByDescending(b => b.ViewCount)  // Trier par le compteur de vues
            .Select(b => new {
                b.Id,
                b.Title,
                Author = b.Author.Name,
                b.CoverImagePath,
                b.UploadDate,
                b.ViewCount  // Inclure le nombre de vues dans la réponse
            })
            .ToList();

        return Ok(booksMagazines);
    }

    [HttpGet("search/popular-downloads")]
    public IActionResult SearchBooksMagazinesByDownloads()
    {
        var booksMagazines = _context.BooksMagazines
            .OrderByDescending(b => b.DownloadCount)  // Trier par le compteur de téléchargements
            .Select(b => new {
                b.Id,
                b.Title,
                Author = b.Author.Name,
                b.CoverImagePath,
                b.UploadDate,
                b.DownloadCount  // Inclure le nombre de téléchargements dans la réponse
            })
            .ToList();

        return Ok(booksMagazines);
    }

    [HttpGet("suggestions")]
    [Authorize]
    public IActionResult GetSuggestions()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Obtenez les catégories des livres déjà lus par l'utilisateur
        var categories = _context.UserReadingHistory
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.BookMagazine.CategoryId)
            .Distinct()
            .ToList();

        // Obtenez les suggestions basées sur ces catégories
        var suggestions = _context.BooksMagazines
            .Where(b => categories.Contains(b.CategoryId))
            .Select(b => new {
                b.Id,
                b.Title,
                Author = b.Author.Name,
                b.CoverImagePath,
                b.UploadDate
            })
            .ToList();

        return Ok(suggestions);
    }


    [HttpGet("download-cover/{id}")]
    public async Task<IActionResult> DownloadCoverImage(int id)
    {
        // Récupérer le livre ou magazine avec l'ID donné
        var bookMagazine = await _context.BooksMagazines
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bookMagazine == null)
            return NotFound("Book or magazine not found.");

        // Vérifier si le chemin de l'image de couverture existe
        if (string.IsNullOrEmpty(bookMagazine.CoverImagePath))
            return NotFound("Cover image not found.");

        // Construire le chemin complet du fichier
        var coverImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", bookMagazine.CoverImagePath.TrimStart('/'));

        // Vérifier si le fichier existe sur le serveur
        if (!System.IO.File.Exists(coverImagePath))
            return NotFound("Cover image file not found on server.");

        // Récupérer les octets du fichier
        var fileBytes = System.IO.File.ReadAllBytes(coverImagePath);

        // Utiliser le nom original de l'image pour le téléchargement
        var originalFileName = bookMagazine.OriginalCoverImageName ?? "cover.jpg";  // Utiliser le nom original ou un nom par défaut

        // Retourner le fichier avec le nom original
        return File(fileBytes, "image/jpeg", originalFileName);
    }



}
