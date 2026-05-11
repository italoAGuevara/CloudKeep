using API.Services.Interfaces;
using HostedService.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Seeding;

public static class TrabajoDemoSeed
{
    public static void EnsureDemoTrabajo(AppDbContext db, IDestinoCredentialProtector credentialProtector)
    {
        //if (!db.Destinos.Any())
        //{
        //    db.Destinos.Add(new Destino
        //    {
        //        Nombre = "Destino demo S3",
        //        TipoDeDestino = "S3",
        //        Credenciales = credentialProtector.Protect("{}"),
        //        CarpetaDestino = "demo-respaldos/"
        //    });
        //    db.SaveChanges();
        //}

        if (db.Trabajos.Any())
            return;

        var origenDoc = db.Origenes.AsNoTracking().FirstOrDefault(o => o.Nombre == "Documentos");
        var destino = db.Destinos.AsNoTracking().First();
        if (origenDoc is null)
            return;

        var link = db.TrabajosOrigenDestinos
            .FirstOrDefault(t => t.OrigenId == origenDoc.Id && t.DestinoId == destino.Id);
        if (link is null)
        {
            link = new TrabajosOrigenDestino { OrigenId = origenDoc.Id, DestinoId = destino.Id };
            db.TrabajosOrigenDestinos.Add(link);
            db.SaveChanges();
        }

        var scriptPre = db.ScriptConfigurations.AsNoTracking().FirstOrDefault(s => s.Nombre == "Notificar inicio");
        var scriptPost = db.ScriptConfigurations.AsNoTracking().FirstOrDefault(s => s.Nombre == "Notificar fin");
        if (scriptPre is null || scriptPost is null)
            return;

        var bundle = db.TrabajosScripts.FirstOrDefault(ts =>
            ts.ScriptPreId == scriptPre.Id
            && ts.ScriptPostId == scriptPost.Id
            && !ts.PreDetenerEnFallo
            && !ts.PostDetenerEnFallo);
        if (bundle is null)
        {
            bundle = new TrabajoScripts
            {
                ScriptPreId = scriptPre.Id,
                ScriptPostId = scriptPost.Id,
                PreDetenerEnFallo = false,
                PostDetenerEnFallo = false
            };
            db.TrabajosScripts.Add(bundle);
            db.SaveChanges();
        }

        db.Trabajos.Add(new Trabajo
        {
            Nombre = "Backup diario documentos",
            Descripcion = "Respaldo de la carpeta Documentos al destino configurado.",
            TrabajosOrigenDestinoId = link.Id,
            TrabajosScriptsId = bundle.Id,
            CronExpression = "0 2 * * *",
            Activo = true
        });
        db.SaveChanges();
    }
}
